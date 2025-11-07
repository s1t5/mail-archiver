using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Utilities;
using MailKit.Net.Imap;
using MailKit;
using MimeKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Compression;

namespace MailArchiver.Services
{
    public class EmlImportService : BackgroundService, IEmlImportService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmlImportService> _logger;
        private readonly BatchOperationOptions _batchOptions;
        private readonly ConcurrentQueue<EmlImportJob> _jobQueue = new();
        private readonly ConcurrentDictionary<string, EmlImportJob> _allJobs = new();
        private readonly Timer _cleanupTimer;
        private CancellationTokenSource? _currentJobCancellation;
        private readonly string _uploadsPath;

        public EmlImportService(IServiceProvider serviceProvider, ILogger<EmlImportService> logger, IWebHostEnvironment environment, IOptions<BatchOperationOptions> batchOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _batchOptions = batchOptions.Value;
            _uploadsPath = Path.Combine(environment.ContentRootPath, "uploads", "eml");

            // Erstelle Upload-Verzeichnis falls es nicht existiert
            Directory.CreateDirectory(_uploadsPath);

            // Cleanup-Timer: Jeden Tag alte Jobs und Dateien entfernen
            _cleanupTimer = new Timer(
                callback: _ => CleanupOldJobs(),
                state: null,
                dueTime: TimeSpan.FromHours(24),
                period: TimeSpan.FromHours(24)
            );
        }

        public string QueueImport(EmlImportJob job)
        {
            job.Status = EmlImportJobStatus.Queued;
            _allJobs[job.JobId] = job;
            _jobQueue.Enqueue(job);
            _logger.LogInformation("Queued EML import job {JobId} for file {FileName} ({FileSize} bytes)",
                job.JobId, job.FileName, job.FileSize);
            return job.JobId;
        }

        public EmlImportJob? GetJob(string jobId)
        {
            return _allJobs.TryGetValue(jobId, out var job) ? job : null;
        }

        public List<EmlImportJob> GetActiveJobs()
        {
            return _allJobs.Values
                .Where(j => j.Status == EmlImportJobStatus.Queued || j.Status == EmlImportJobStatus.Running)
                .OrderBy(j => j.Created)
                .ToList();
        }

        public List<EmlImportJob> GetAllJobs()
        {
            return _allJobs.Values
                .OrderByDescending(j => j.Status == EmlImportJobStatus.Running || j.Status == EmlImportJobStatus.Queued)
                .ThenByDescending(j => j.Created)
                .ToList();
        }

        public bool CancelJob(string jobId)
        {
            if (_allJobs.TryGetValue(jobId, out var job))
            {
                if (job.Status == EmlImportJobStatus.Queued)
                {
                    job.Status = EmlImportJobStatus.Cancelled;
                    job.Completed = DateTime.UtcNow;
                    
                    // Delete the temporary file for queued jobs
                    DeleteTempFile(job.FilePath, jobId);
                    
                    _logger.LogInformation("Cancelled queued EML import job {JobId}", jobId);
                    return true;
                }
                else if (job.Status == EmlImportJobStatus.Running)
                {
                    job.Status = EmlImportJobStatus.Cancelled;
                    _currentJobCancellation?.Cancel();
                    _logger.LogInformation("Requested cancellation of running EML import job {JobId}", jobId);
                    return true;
                }
            }
            return false;
        }

        public async Task<string> SaveUploadedFileAsync(IFormFile file)
        {
            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(_uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("Saved uploaded EML file to {FilePath}", filePath);
            return filePath;
        }

        public async Task<int> EstimateEmailCountAsync(string filePath)
        {
            try
            {
                using var zip = ZipFile.OpenRead(filePath);
                int count = zip.Entries.Count(e => e.Name.EndsWith(".eml", StringComparison.OrdinalIgnoreCase));
                _logger.LogInformation("Estimated {Count} emails in ZIP file {FilePath}", count, filePath);
                return count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estimating email count for file {FilePath}", filePath);
                return 0;
            }
        }

        public void CleanupOldJobs()
        {
            var cutoffTime = DateTime.UtcNow.AddDays(-7); // Jobs älter als 7 Tage entfernen
            var toRemove = _allJobs.Values
                .Where(j => j.Completed.HasValue && j.Completed < cutoffTime)
                .ToList();

            foreach (var job in toRemove)
            {
                _allJobs.TryRemove(job.JobId, out _);

                // Lösche auch die zugehörige Datei
                try
                {
                    if (File.Exists(job.FilePath))
                    {
                        File.Delete(job.FilePath);
                        _logger.LogInformation("Deleted old EML file {FilePath}", job.FilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old EML file {FilePath}", job.FilePath);
                }
            }

            if (toRemove.Any())
            {
                _logger.LogInformation("Cleaned up {Count} old EML import jobs", toRemove.Count);
            }
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("EML Import Background Service is starting.");
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("EML Import Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_jobQueue.TryDequeue(out var job))
                    {
                        if (job.Status == EmlImportJobStatus.Cancelled)
                        {
                            _logger.LogInformation("Skipping cancelled EML import job {JobId}", job.JobId);
                            continue;
                        }

                        await ProcessJob(job, stoppingToken);
                    }
                    else
                    {
                        // Kürzere Wartezeit für bessere Reaktionsfähigkeit
                        await Task.Delay(100, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("EML Import Background Service stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in EML Import Background Service");
                    // Kürzere Wartezeit nach Fehler
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("EML Import Background Service is stopping.");
            return base.StopAsync(cancellationToken);
        }

        private async Task ProcessJob(EmlImportJob job, CancellationToken stoppingToken)
        {
            _currentJobCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var cancellationToken = _currentJobCancellation.Token;

            try
            {
                job.Status = EmlImportJobStatus.Running;
                job.Started = DateTime.UtcNow;

                _logger.LogInformation("Starting EML import job {JobId} for file {FileName}",
                    job.JobId, job.FileName);

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

                // Lade Target Account
                var targetAccount = await context.MailAccounts.FindAsync(job.TargetAccountId);
                if (targetAccount == null)
                {
                    throw new InvalidOperationException($"Target account {job.TargetAccountId} not found");
                }

                // Schätze E-Mail-Anzahl wenn noch nicht vorhanden
                if (job.TotalEmails == 0)
                {
                    job.TotalEmails = await EstimateEmailCountAsync(job.FilePath);
                }

                // Verarbeite ZIP-Datei mit EML-Dateien
                await ProcessZipFile(job, targetAccount, cancellationToken);

                if (job.Status != EmlImportJobStatus.Cancelled)
                {
                    job.Status = EmlImportJobStatus.Completed;
                    job.Completed = DateTime.UtcNow;
                    var skippedCount = job.ProcessedEmails - job.SuccessCount - job.FailedCount;
                    _logger.LogInformation("Completed EML import job {JobId}. Success: {Success}, Failed: {Failed}, Skipped (already exists): {Skipped}",
                        job.JobId, job.SuccessCount, job.FailedCount, skippedCount);
                }

                // Lösche die temporäre Datei nach erfolgreichem Import
                try
                {
                    if (File.Exists(job.FilePath))
                    {
                        File.Delete(job.FilePath);
                        _logger.LogInformation("Deleted temporary EML file {FilePath}", job.FilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary EML file {FilePath}", job.FilePath);
                }
            }
            catch (OperationCanceledException)
            {
                job.Status = EmlImportJobStatus.Cancelled;
                job.Completed = DateTime.UtcNow;
                
                // Delete the temporary file for cancelled running jobs
                DeleteTempFile(job.FilePath, job.JobId);
                
                _logger.LogInformation("EML import job {JobId} was cancelled", job.JobId);
            }
            catch (Exception ex)
            {
                job.Status = EmlImportJobStatus.Failed;
                job.Completed = DateTime.UtcNow;
                job.ErrorMessage = ex.Message;
                _logger.LogError(ex, "EML import job {JobId} failed", job.JobId);
            }
            finally
            {
                _currentJobCancellation?.Dispose();
                _currentJobCancellation = null;
            }
        }

        private async Task ProcessZipFile(EmlImportJob job, MailAccount targetAccount, CancellationToken cancellationToken)
        {
            try
            {
                using var zip = ZipFile.OpenRead(job.FilePath);
                var emlEntries = zip.Entries.Where(e => e.Name.EndsWith(".eml", StringComparison.OrdinalIgnoreCase)).ToList();
                job.TotalEmails = emlEntries.Count;

                foreach (var entry in emlEntries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        // Bestimme den Zielordner basierend auf dem Pfad innerhalb der ZIP-Datei
                        string targetFolder = "INBOX";
                        if (!string.IsNullOrEmpty(entry.FullName))
                        {
                            // Entferne den Dateinamen, um nur den Ordnerpfad zu erhalten
                            var folderPath = Path.GetDirectoryName(entry.FullName);
                            if (!string.IsNullOrEmpty(folderPath))
                            {
                                // Verwende den letzten Teil des Pfades als Ordnernamen
                                var folders = folderPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                                if (folders.Length > 0)
                                {
                                    targetFolder = folders[folders.Length - 1];
                                }
                            }
                        }

                        using var entryStream = entry.Open();
                        using var memoryStream = new MemoryStream();
                        await entryStream.CopyToAsync(memoryStream, cancellationToken);
                        memoryStream.Position = 0;

                        var parser = new MimeParser(memoryStream, MimeFormat.Entity);
                        var message = await parser.ParseMessageAsync(cancellationToken);

                        job.CurrentEmailSubject = message.Subject;
                        job.ProcessedBytes = memoryStream.Position;

                        // Importiere E-Mail in die Datenbank
                        var importResult = await ImportEmailToDatabase(message, targetAccount, job, targetFolder);

                        // Explicitly dispose of the message to free memory
                        message?.Dispose();

                        if (importResult.Success)
                        {
                            job.SuccessCount++;
                        }
                        else if (importResult.AlreadyExists)
                        {
                            // Bereits vorhandene E-Mails als skipped zählen, nicht als failed
                        }
                        else
                        {
                            job.FailedCount++;
                        }

                        job.ProcessedEmails++;

                        // Clear Entity Framework Change Tracker every 50 emails to prevent memory accumulation
                        if (job.ProcessedEmails % 50 == 0)
                        {
                            using var scope = _serviceProvider.CreateScope();
                            var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                            context.ChangeTracker.Clear();
                        }

                        // Kleine Pause alle 10 E-Mails
                        if (job.ProcessedEmails % 10 == 0 && _batchOptions.PauseBetweenEmailsMs > 0)
                        {
                            await Task.Delay(_batchOptions.PauseBetweenEmailsMs, cancellationToken);
                            
                            // Force garbage collection after every 10 emails to free memory
                            if (job.ProcessedEmails % 50 == 0)
                            {
                                GC.Collect();
                                GC.WaitForPendingFinalizers();
                            }
                        }

                        // Log Progress alle 100 E-Mails
                        if (job.ProcessedEmails % 100 == 0)
                        {
                            var progressPercent = job.TotalEmails > 0 ? (job.ProcessedEmails * 100.0 / job.TotalEmails) : 0;
                            _logger.LogInformation("Job {JobId}: Processed {Processed} emails ({Progress:F1}%)",
                                job.JobId, job.ProcessedEmails, progressPercent);
                            
                            // Clear Entity Framework Change Tracker for comprehensive cleanup every 100 emails
                            using var scope = _serviceProvider.CreateScope();
                            var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                            context.ChangeTracker.Clear();
                            
                            // Force garbage collection after every 100 emails to free memory
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            
                            // Log memory usage after processing batch
                            _logger.LogInformation("Memory usage after processing batch: {MemoryUsage}",
                                MemoryMonitor.GetMemoryUsageFormatted());
                        }
                    }
                    catch (FormatException ex)
                    {
                        _logger.LogWarning(ex, "Job {JobId}: Skipping malformed email in entry {EntryName}",
                            job.JobId, entry.FullName);
                        job.FailedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Job {JobId}: Error processing email in entry {EntryName}",
                            job.JobId, entry.FullName);
                        job.FailedCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId}: Error processing ZIP file {FilePath}",
                    job.JobId, job.FilePath);
                throw;
            }
        }

        private async Task<ImportResult> ImportEmailToDatabase(MimeMessage message, MailAccount account, EmlImportJob job, string targetFolder)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

                // Verbesserte MessageId-Generierung für Provider ohne zuverlässige MessageIds
                var messageId = message.MessageId;
                
                // Wenn keine MessageId vorhanden, generiere Hash basierend auf E-Mail-Inhalt
                if (string.IsNullOrEmpty(messageId))
                {
                    var hashFrom = string.Join(",", message.From.Mailboxes.Select(m => m.Address));
                    var hashTo = string.Join(",", message.To.Mailboxes.Select(m => m.Address));
                    var hashSubject = message.Subject ?? "";
                    var dateTicks = message.Date.Ticks;
                    
                    var uniqueString = $"{hashFrom}|{hashTo}|{hashSubject}|{dateTicks}";
                    using (var sha256 = System.Security.Cryptography.SHA256.Create())
                    {
                        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(uniqueString));
                        var hashString = Convert.ToBase64String(hashBytes).Replace("+", "-").Replace("/", "_").Substring(0, 16);
                        messageId = $"generated-{hashString}@mail-archiver.local";
                    }
                    
                    _logger.LogDebug("Job {JobId}: Generated MessageId for email without MessageId: {MessageId}", 
                        job.JobId, messageId);
                }

                _logger.LogDebug("Job {JobId}: Processing email with MessageId: {MessageId}, Subject: {Subject}", 
                    job.JobId, messageId, message.Subject ?? "(No Subject)");

                // ROBUSTE Duplikaterkennung - prüfe mehrere Kriterien
                // Yahoo hat oft fehlende/duplizierte MessageIds, daher prüfen wir auch Inhalt
                var checkFrom = string.Join(",", message.From.Mailboxes.Select(m => m.Address));
                var checkTo = string.Join(",", message.To.Mailboxes.Select(m => m.Address));
                var checkSubject = message.Subject ?? "(No Subject)";
                
                var existing = await context.ArchivedEmails
                    .Where(e => e.MailAccountId == account.Id)
                    .Where(e => 
                        e.MessageId == messageId ||
                        (e.From == checkFrom &&
                         e.To == checkTo &&
                         e.Subject == checkSubject &&
                         Math.Abs((e.SentDate - message.Date.DateTime).TotalSeconds) < 2)
                    )
                    .FirstOrDefaultAsync();

                if (existing != null)
                {
                    _logger.LogInformation("Job {JobId}: Email already exists (duplicate) - MessageId: {MessageId}, Subject: {Subject}, From: {From}", 
                        job.JobId, messageId, checkSubject, checkFrom);
                    return ImportResult.CreateAlreadyExists();
                }

                // Extract text and HTML body preserving original encoding
                var body = string.Empty;
                var htmlBody = string.Empty;
                var bodyUntruncatedText = string.Empty;
                var bodyUntruncatedHtml = string.Empty;
                var isHtmlTruncated = false;
                var isBodyTruncated = false;

                // Handle text body - use original content directly to preserve encoding
                if (!string.IsNullOrEmpty(message.TextBody))
                {
                    var cleanedTextBody = CleanText(message.TextBody);
                    // Check if text body needs truncation for tsvector compatibility
                    // Set to 500KB to ensure total of all fields stays under 1MB tsvector limit
                    if (Encoding.UTF8.GetByteCount(cleanedTextBody) > 500_000)
                    {
                        isBodyTruncated = true;
                        // Store original in untruncated column
                        bodyUntruncatedText = cleanedTextBody;
                        // Store truncated version for search index
                        body = TruncateTextForStorage(cleanedTextBody, 500_000);
                    }
                    else
                    {
                        body = cleanedTextBody;
                    }
                }
                else if (!string.IsNullOrEmpty(message.HtmlBody))
                {
                    // If no TextBody, try to extract text from HTML body
                    var cleanedHtmlAsText = CleanText(message.HtmlBody);
                    // Check if HTML-as-text body needs truncation for tsvector compatibility
                    // Set to 500KB to ensure total of all fields stays under 1MB tsvector limit
                    if (Encoding.UTF8.GetByteCount(cleanedHtmlAsText) > 500_000)
                    {
                        isBodyTruncated = true;
                        // Store original in untruncated column
                        bodyUntruncatedText = cleanedHtmlAsText;
                        // Store truncated version for search index
                        body = TruncateTextForStorage(cleanedHtmlAsText, 500_000);
                    }
                    else
                    {
                        body = cleanedHtmlAsText;
                    }
                }

                // Handle HTML body - preserve original encoding
                if (!string.IsNullOrEmpty(message.HtmlBody))
                {
                    var cleanedHtmlBody = CleanText(message.HtmlBody);
                    // Check if HTML body needs truncation
                    if (Encoding.UTF8.GetByteCount(cleanedHtmlBody) > 1_000_000)
                    {
                        isHtmlTruncated = true;
                        // Store original in untruncated column
                        bodyUntruncatedHtml = cleanedHtmlBody;
                        // Store truncated version for search index
                        htmlBody = CleanHtmlForStorage(cleanedHtmlBody);
                    }
                    else
                    {
                        htmlBody = cleanedHtmlBody;
                    }
                }

                // Sammle ALLE Anhänge einschließlich inline Images
                var allAttachments = new List<MimePart>();
                CollectAllAttachments(message.Body, allAttachments);

                // Convert timestamp to configured display timezone
                var dateTimeHelper = scope.ServiceProvider.GetRequiredService<DateTimeHelper>();
                var convertedSentDate = dateTimeHelper.ConvertToDisplayTimeZone(message.Date);

                var subject = CleanText(message.Subject ?? "(No Subject)");
                var from = CleanText(string.Join(", ", message.From.Mailboxes.Select(m => m.Address)));
                var to = CleanText(string.Join(", ", message.To.Mailboxes.Select(m => m.Address)));
                var cc = CleanText(string.Join(", ", message.Cc?.Mailboxes.Select(m => m.Address) ?? Enumerable.Empty<string>()));
                var bcc = CleanText(string.Join(", ", message.Bcc?.Mailboxes.Select(m => m.Address) ?? Enumerable.Empty<string>()));

                // Ensure individual fields don't exceed reasonable limits for tsvector
                subject = TruncateFieldForTsvector(subject, 50_000);
                from = TruncateFieldForTsvector(from, 10_000);
                to = TruncateFieldForTsvector(to, 50_000);
                cc = TruncateFieldForTsvector(cc, 50_000);
                bcc = TruncateFieldForTsvector(bcc, 50_000);
                
                // Final safety check: ensure total size for tsvector doesn't exceed limit
                var totalTsvectorSize = Encoding.UTF8.GetByteCount(subject) +
                                       Encoding.UTF8.GetByteCount(body) +
                                       Encoding.UTF8.GetByteCount(from) +
                                       Encoding.UTF8.GetByteCount(to) +
                                       Encoding.UTF8.GetByteCount(cc) +
                                       Encoding.UTF8.GetByteCount(bcc);

                const int maxTsvectorSize = 900_000;
                if (totalTsvectorSize > maxTsvectorSize)
                {
                    _logger.LogWarning("Job {JobId}: Email fields exceed tsvector limit ({TotalSize} > {MaxSize}), truncating body further",
                        job.JobId, totalTsvectorSize, maxTsvectorSize);
                    
                    var otherFieldsSize = totalTsvectorSize - Encoding.UTF8.GetByteCount(body);
                    var maxBodySize = maxTsvectorSize - otherFieldsSize - 10_000;
                    
                    if (maxBodySize > 0 && Encoding.UTF8.GetByteCount(body) > maxBodySize)
                    {
                        isBodyTruncated = true;
                        body = TruncateTextForStorage(body, maxBodySize);
                    }
                    else if (maxBodySize <= 0)
                    {
                        _logger.LogError("Job {JobId}: Other email fields alone exceed tsvector limit", job.JobId);
                        isBodyTruncated = true;
                        body = "[Body too large - saved as attachment]";
                    }
                }

                var archivedEmail = new ArchivedEmail
                {
                    MailAccountId = account.Id,
                    MessageId = messageId,
                    Subject = subject,
                    From = from,
                    To = to,
                    Cc = cc,
                    Bcc = bcc,
                    SentDate = convertedSentDate,
                    ReceivedDate = DateTime.UtcNow,
                    IsOutgoing = DetermineIfOutgoing(message, account),
                    HasAttachments = allAttachments.Any(),
                    Body = body,
                    HtmlBody = htmlBody,
                    BodyUntruncatedText = !string.IsNullOrEmpty(bodyUntruncatedText) ? bodyUntruncatedText : null,
                    BodyUntruncatedHtml = !string.IsNullOrEmpty(bodyUntruncatedHtml) ? bodyUntruncatedHtml : null,
                    FolderName = targetFolder,
                    Attachments = new List<EmailAttachment>() // Initialize collection for hash calculation
                };

                _logger.LogDebug("Job {JobId}: Adding email to database: {MessageId}", job.JobId, messageId);
                context.ArchivedEmails.Add(archivedEmail);
                await context.SaveChangesAsync();
                _logger.LogDebug("Job {JobId}: Successfully saved email: {MessageId}", job.JobId, messageId);

                // Speichere ALLE Anhänge als normale Attachments
                if (allAttachments.Any())
                {
                    _logger.LogDebug("Job {JobId}: Saving {Count} attachments for email: {MessageId}", 
                        job.JobId, allAttachments.Count, messageId);
                    await SaveAllAttachments(context, allAttachments, archivedEmail.Id);
                }

                _logger.LogDebug("Job {JobId}: Successfully imported email: {MessageId}, Truncated: {IsTruncated}", 
                    job.JobId, messageId, isHtmlTruncated || isBodyTruncated ? "Yes" : "No");
                return ImportResult.CreateSuccess();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId}: Failed to import email to database. Subject: {Subject}, MessageId: {MessageId}, Error: {Error}", 
                    job.JobId, message.Subject ?? "(No Subject)", message.MessageId ?? "None", ex.Message);
                return ImportResult.CreateFailed(ex.Message);
            }
        }

        // Hilfsmethoden für EML Import - umfassende Inline-Image-Erkennung
        private void CollectAllAttachments(MimeEntity entity, List<MimePart> attachments)
        {
            _logger.LogDebug("CollectAllAttachments: Processing entity type: {EntityType}", entity.GetType().Name);

            if (entity is MimePart mimePart)
            {
                _logger.LogDebug("Processing MimePart: ContentType={ContentType}, FileName={FileName}, ContentId={ContentId}, IsAttachment={IsAttachment}, ContentDisposition={ContentDisposition}",
                    mimePart.ContentType?.MimeType, mimePart.FileName, mimePart.ContentId, mimePart.IsAttachment, mimePart.ContentDisposition?.Disposition);

                // Sammle normale Attachments
                if (mimePart.IsAttachment)
                {
                    attachments.Add(mimePart);
                    _logger.LogDebug("Found attachment: FileName={FileName}, ContentType={ContentType}",
                        mimePart.FileName, mimePart.ContentType?.MimeType);
                }
                // Sammle inline Images und andere inline Content
                else if (IsInlineContent(mimePart))
                {
                    attachments.Add(mimePart);
                    _logger.LogDebug("Found inline content: ContentId={ContentId}, ContentType={ContentType}, FileName={FileName}",
                        mimePart.ContentId, mimePart.ContentType?.MimeType, mimePart.FileName);
                }
                else
                {
                    _logger.LogDebug("Skipping MimePart: Not attachment and not inline content");
                }
            }
            else if (entity is Multipart multipart)
            {
                _logger.LogDebug("Processing Multipart with {Count} children", multipart.Count);
                // Rekursiv durch alle Teile einer Multipart-Nachricht gehen
                foreach (var child in multipart)
                {
                    CollectAllAttachments(child, attachments);
                }
            }
            else if (entity is MessagePart messagePart)
            {
                _logger.LogDebug("Processing MessagePart");
                // Auch in eingebetteten Nachrichten suchen
                CollectAllAttachments(messagePart.Message.Body, attachments);
            }
            else
            {
                _logger.LogDebug("Skipping entity type: {EntityType}", entity.GetType().Name);
            }
        }

        /// <summary>
        /// Umfassende Erkennung von Inline-Content für EML Import
        /// </summary>
        private bool IsInlineContent(MimePart mimePart)
        {
            // Primäre Prüfung: Content-Disposition = inline
            if (mimePart.ContentDisposition?.Disposition?.Equals("inline", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogDebug("Found inline content via Content-Disposition: ContentId={ContentId}, ContentType={ContentType}, FileName={FileName}",
                    mimePart.ContentId, mimePart.ContentType?.MimeType, mimePart.FileName);
                return true;
            }

            // Sekundäre Prüfung: Content-ID vorhanden (klassisches Inline-Image)
            if (!string.IsNullOrEmpty(mimePart.ContentId))
            {
                _logger.LogDebug("Found inline content via Content-ID: ContentId={ContentId}, ContentType={ContentType}, FileName={FileName}",
                    mimePart.ContentId, mimePart.ContentType?.MimeType, mimePart.FileName);
                return true;
            }

            // Tertiäre Prüfung: Image ohne Content-Disposition oder mit generic filename
            var contentType = mimePart.ContentType?.MimeType?.ToLowerInvariant() ?? "";
            var fileName = mimePart.FileName ?? "";
            
            if (contentType.StartsWith("image/"))
            {
                // Bilder ohne Content-Disposition sind oft inline
                if (mimePart.ContentDisposition == null)
                {
                    _logger.LogDebug("Found potential inline image (no ContentDisposition): ContentType={ContentType}, FileName={FileName}",
                        mimePart.ContentType?.MimeType, fileName);
                    return true;
                }

                // Bilder mit generischen Dateinamen sind oft inline
                if (string.IsNullOrEmpty(fileName) || 
                    fileName.StartsWith("image", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Contains("inline", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Contains("embed", StringComparison.OrdinalIgnoreCase) ||
                    Regex.IsMatch(fileName, @"^(img|pic|photo)\d*\.", RegexOptions.IgnoreCase))
                {
                    _logger.LogDebug("Found potential inline image (generic filename): ContentType={ContentType}, FileName={FileName}",
                        mimePart.ContentType?.MimeType, fileName);
                    return true;
                }
            }

            // Quartiäre Prüfung: RFC2387 related content
            if (contentType.StartsWith("text/") && contentType.Contains("related"))
            {
                _logger.LogDebug("Found potential inline content (related text): ContentType={ContentType}, FileName={FileName}",
                    mimePart.ContentType?.MimeType, fileName);
                return true;
            }

            return false;
        }

        private async Task SaveAllAttachments(MailArchiverDbContext context, List<MimePart> attachments, int archivedEmailId)
        {
            foreach (var attachment in attachments)
            {
                try
                {
                    using var ms = new MemoryStream();
                    await attachment.Content.DecodeToAsync(ms);

                    var fileName = attachment.FileName;
                    if (string.IsNullOrEmpty(fileName))
                    {
                        if (!string.IsNullOrEmpty(attachment.ContentId))
                        {
                            var extension = GetFileExtensionFromContentType(attachment.ContentType?.MimeType);
                            var cleanContentId = attachment.ContentId.Trim('<', '>');
                            fileName = $"inline_{cleanContentId}{extension}";
                        }
                        else
                        {
                            var extension = GetFileExtensionFromContentType(attachment.ContentType?.MimeType);
                            fileName = $"attachment_{Guid.NewGuid().ToString("N")[..8]}{extension}";
                        }
                    }

                    var emailAttachment = new EmailAttachment
                    {
                        ArchivedEmailId = archivedEmailId,
                        FileName = CleanText(fileName),
                        ContentType = CleanText(attachment.ContentType?.MimeType ?? "application/octet-stream"),
                        ContentId = !string.IsNullOrEmpty(attachment.ContentId) ? CleanText(attachment.ContentId) : null,
                        Content = ms.ToArray(),
                        Size = ms.Length
                    };

                    context.EmailAttachments.Add(emailAttachment);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save attachment {FileName}", attachment.FileName);
                }
            }

            try
            {
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save attachments for email");
            }
        }

        /// <summary>
        /// Saves the original HTML content as an attachment when it was truncated
        /// </summary>
        /// <param name="context">The database context</param>
        /// <param name="originalHtml">The original HTML content</param>
        /// <param name="archivedEmailId">The email ID to attach to</param>
        /// <param name="jobId">The job ID for logging</param>
        /// <param name="messageId">The message ID for logging</param>
        /// <returns>Task</returns>
        private async Task SaveTruncatedHtmlAsAttachment(MailArchiverDbContext context, string originalHtml, int archivedEmailId, string jobId, string messageId)
        {
            try
            {
                // Get the email's attachments to resolve inline images
                var email = await context.ArchivedEmails
                    .Include(e => e.Attachments)
                    .FirstOrDefaultAsync(e => e.Id == archivedEmailId);

                if (email != null && email.Attachments != null && email.Attachments.Any())
                {
                    // Resolve inline images by converting cid: references to data URLs
                    originalHtml = ResolveInlineImagesInHtml(originalHtml, email.Attachments.ToList(), jobId);
                }

                var cleanFileName = CleanText($"original_content_{DateTime.UtcNow:yyyyMMddHHmmss}.html");
                var contentType = "text/html";

                var emailAttachment = new EmailAttachment
                {
                    ArchivedEmailId = archivedEmailId,
                    FileName = cleanFileName,
                    ContentType = contentType,
                    Content = Encoding.UTF8.GetBytes(originalHtml),
                    Size = Encoding.UTF8.GetByteCount(originalHtml)
                };

                context.EmailAttachments.Add(emailAttachment);
                await context.SaveChangesAsync();

                _logger.LogInformation("Job {JobId}: Successfully saved original HTML content as attachment for email {MessageId} with {ImageCount} inline images resolved",
                    jobId, messageId, email?.Attachments?.Count(a => !string.IsNullOrEmpty(a.ContentId)) ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId}: Failed to save original HTML content as attachment for email {MessageId}", jobId, messageId);
            }
        }

        /// <summary>
        /// Resolves inline images in HTML by converting cid: references to data URLs
        /// </summary>
        private string ResolveInlineImagesInHtml(string htmlBody, List<EmailAttachment> attachments, string jobId)
        {
            if (string.IsNullOrEmpty(htmlBody) || attachments == null || !attachments.Any())
                return htmlBody;

            var resultHtml = htmlBody;

            // Find all cid: references in the HTML
            var cidMatches = Regex.Matches(htmlBody, @"src\s*=\s*[""']cid:([^""']+)[""']", RegexOptions.IgnoreCase);

            foreach (Match match in cidMatches)
            {
                var cid = match.Groups[1].Value;

                // Find the corresponding attachment - try multiple matching strategies
                var attachment = attachments.FirstOrDefault(a => 
                    !string.IsNullOrEmpty(a.ContentId) && 
                    (a.ContentId.Equals($"<{cid}>", StringComparison.OrdinalIgnoreCase) ||
                     a.ContentId.Equals(cid, StringComparison.OrdinalIgnoreCase)));

                // If no attachment with ContentId found, try matching by filename
                if (attachment == null)
                {
                    attachment = attachments.FirstOrDefault(a => 
                        !string.IsNullOrEmpty(a.FileName) && 
                        (a.FileName.Equals($"inline_{cid}", StringComparison.OrdinalIgnoreCase) ||
                         a.FileName.StartsWith($"inline_{cid}.", StringComparison.OrdinalIgnoreCase) ||
                         a.FileName.Contains($"_{cid}")));
                }

                if (attachment != null && attachment.Content != null && attachment.Content.Length > 0)
                {
                    try
                    {
                        // Create a data URL for the inline image
                        var base64Content = Convert.ToBase64String(attachment.Content);
                        var dataUrl = $"data:{attachment.ContentType ?? "image/png"};base64,{base64Content}";
                        
                        // Replace the cid: reference with the data URL
                        resultHtml = resultHtml.Replace(match.Groups[0].Value, $"src=\"{dataUrl}\"");
                        
                        _logger.LogDebug("Job {JobId}: Resolved inline image with CID: {Cid} to data URL", jobId, cid);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Job {JobId}: Failed to resolve inline image with CID: {Cid}", jobId, cid);
                    }
                }
                else
                {
                    _logger.LogWarning("Job {JobId}: Could not find attachment for CID: {Cid}", jobId, cid);
                }
            }

            return resultHtml;
        }

        /// <summary>
        /// Saves the original text content as an attachment when it was truncated
        /// </summary>
        /// <param name="context">The database context</param>
        /// <param name="originalText">The original text content</param>
        /// <param name="archivedEmailId">The email ID to attach to</param>
        /// <param name="jobId">The job ID for logging</param>
        /// <param name="messageId">The message ID for logging</param>
        /// <returns>Task</returns>
        private async Task SaveTruncatedTextAsAttachment(MailArchiverDbContext context, string originalText, int archivedEmailId, string jobId, string messageId)
        {
            try
            {
                var cleanFileName = CleanText($"original_text_content_{DateTime.UtcNow:yyyyMMddHHmmss}.txt");
                var contentType = "text/plain";

                var emailAttachment = new EmailAttachment
                {
                    ArchivedEmailId = archivedEmailId,
                    FileName = cleanFileName,
                    ContentType = contentType,
                    Content = Encoding.UTF8.GetBytes(originalText),
                    Size = Encoding.UTF8.GetByteCount(originalText)
                };

                context.EmailAttachments.Add(emailAttachment);
                await context.SaveChangesAsync();

                _logger.LogInformation("Job {JobId}: Successfully saved original text content as attachment for email {MessageId}",
                    jobId, messageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job {JobId}: Failed to save original text content as attachment for email {MessageId}", jobId, messageId);
            }
        }

        private string GetFileExtensionFromContentType(string? contentType)
        {
            return contentType?.ToLowerInvariant() switch
            {
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                "image/webp" => ".webp",
                "image/svg+xml" => ".svg",
                _ => ".dat"
            };
        }

        private bool DetermineIfOutgoing(MimeMessage message, MailAccount account)
        {
            // Prüfe ob die E-Mail vom Account gesendet wurde
            var accountEmail = account.EmailAddress.ToLowerInvariant();
            var fromAddress = message.From.Mailboxes.FirstOrDefault()?.Address?.ToLowerInvariant();

            return fromAddress == accountEmail;
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            text = text.Replace("\0", "");
            var cleanedText = new StringBuilder();

            foreach (var c in text)
            {
                if (c < 32 && c != '\r' && c != '\n' && c != '\t')
                {
                    cleanedText.Append(' ');
                }
                else
                {
                    cleanedText.Append(c);
                }
            }

            return cleanedText.ToString();
        }

        // Constants for HTML truncation - calculated once to avoid repeated computations
        private static readonly string TruncationNotice = @"
                    <div style='background-color: #f8f9fa; border: 1px solid #dee2e6; border-radius: 5px; padding: 15px; margin: 10px 0; font-family: Arial, sans-serif;'>
                        <h4 style='color: #495057; margin-top: 0;'>📎 Email content has been truncated</h4>
                        <p style='color: #6c757d; margin-bottom: 10px;'>
                            This email contains very large HTML content (over 1 MB) that has been truncated for better performance.
                        </p>
                        <p style='color: #6c757d; margin-bottom: 0;'>
                            <strong>The complete original HTML content has been saved as an attachment.</strong><br>
                            Look for a file named 'original_content_*.html' in the attachments.
                        </p>
                    </div>";
        
        private static readonly int TruncationOverhead = Encoding.UTF8.GetByteCount(TruncationNotice + "</body></html>");
        private const int MaxHtmlSizeBytes = 1_000_000;

        private string CleanHtmlForStorage(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            // Remove null characters efficiently - only if they exist
            if (html.Contains('\0'))
            {
                html = html.Replace("\0", "");
            }

            // Early return for small HTML content
            if (html.Length <= MaxHtmlSizeBytes)
                return html;

            // Calculate safe truncation position
            int maxContentSize = MaxHtmlSizeBytes - TruncationOverhead;
            if (maxContentSize <= 0)
            {
                // Fallback for edge case - return minimal valid HTML
                return $"<html><body>{TruncationNotice}</body></html>";
            }

            int truncatePosition = Math.Min(maxContentSize, html.Length);

            // Find safe truncation point that doesn't break HTML tags
            int lastLessThan = html.LastIndexOf('<', truncatePosition - 1);
            int lastGreaterThan = html.LastIndexOf('>', truncatePosition - 1);
            
            // If we're inside a tag, truncate before it starts
            if (lastLessThan > lastGreaterThan && lastLessThan >= 0)
            {
                truncatePosition = lastLessThan;
            }
            else if (lastGreaterThan >= 0)
            {
                // Otherwise, truncate after the last complete tag
                truncatePosition = lastGreaterThan + 1;
            }

            // Use StringBuilder for efficient string building
            var result = new StringBuilder(truncatePosition + TruncationNotice.Length + 50);
            
            // Get base content as span for better performance
            ReadOnlySpan<char> baseContent = html.AsSpan(0, truncatePosition);
            
            // Check for HTML structure efficiently
            bool hasHtml = baseContent.Contains("<html".AsSpan(), StringComparison.OrdinalIgnoreCase);
            bool hasBody = baseContent.Contains("<body".AsSpan(), StringComparison.OrdinalIgnoreCase);

            // Build the result efficiently
            if (!hasHtml)
            {
                result.Append("<html>");
            }

            if (!hasBody)
            {
                if (hasHtml)
                {
                    // Find where to insert <body> tag efficiently
                    string contentStr = baseContent.ToString();
                    int htmlStart = contentStr.IndexOf("<html", StringComparison.OrdinalIgnoreCase);
                    if (htmlStart >= 0)
                    {
                        int htmlTagEnd = contentStr.IndexOf('>', htmlStart);
                        if (htmlTagEnd >= 0)
                        {
                            result.Append(baseContent.Slice(0, htmlTagEnd + 1));
                            result.Append("<body>");
                            result.Append(baseContent.Slice(htmlTagEnd + 1));
                        }
                        else
                        {
                            result.Append("<body>");
                            result.Append(baseContent);
                        }
                    }
                    else
                    {
                        result.Append("<body>");
                        result.Append(baseContent);
                    }
                }
                else
                {
                    result.Append("<body>");
                    result.Append(baseContent);
                }
            }
            else
            {
                result.Append(baseContent);
            }

            // Add truncation notice
            result.Append(TruncationNotice);

            // Close tags efficiently
            string resultStr = result.ToString();
            if (!resultStr.EndsWith("</body>", StringComparison.OrdinalIgnoreCase))
            {
                result.Append("</body>");
            }
            if (!resultStr.EndsWith("</html>", StringComparison.OrdinalIgnoreCase))
            {
                result.Append("</html>");
            }

            return result.ToString();
        }

        private string TruncateFieldForTsvector(string text, int maxSizeBytes)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (Encoding.UTF8.GetByteCount(text) <= maxSizeBytes)
                return text;

            int approximateCharPosition = Math.Min(maxSizeBytes, text.Length);

            while (approximateCharPosition > 0 && Encoding.UTF8.GetByteCount(text.Substring(0, approximateCharPosition)) > maxSizeBytes)
            {
                approximateCharPosition--;
            }

            int wordBoundarySearch = Math.Max(0, approximateCharPosition - 50);
            int lastSpaceIndex = text.LastIndexOf(' ', approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);

            if (lastSpaceIndex > wordBoundarySearch)
            {
                approximateCharPosition = lastSpaceIndex;
            }

            return text.Substring(0, approximateCharPosition) + "...";
        }

        /// <summary>
        /// Truncates text content to fit within tsvector size limits while preserving readability
        /// </summary>
        /// <param name="text">The text to truncate</param>
        /// <param name="maxSizeBytes">Maximum size in bytes</param>
        /// <returns>Truncated text with notice appended</returns>
        private string TruncateTextForStorage(string text, int maxSizeBytes)
        {
            
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            const string textTruncationNotice = "\n\n[CONTENT TRUNCATED - This email contains very large text content that has been truncated for better performance. The complete original content has been saved as an attachment.]";
            
            // Calculate overhead for the truncation notice
            int noticeOverhead = Encoding.UTF8.GetByteCount(textTruncationNotice);
            int maxContentSize = maxSizeBytes - noticeOverhead;
            
            if (maxContentSize <= 0)
            {
                // Edge case - just return the notice
                return textTruncationNotice;
            }

            // Check if we need to truncate
            if (Encoding.UTF8.GetByteCount(text) <= maxSizeBytes)
            {
                return text; // No truncation needed
            }

            // Find a safe truncation point that doesn't break in the middle of a word
            int approximateCharPosition = Math.Min(maxContentSize, text.Length);
            
            // Work backwards to find a word boundary or reasonable break point
            while (approximateCharPosition > 0 && Encoding.UTF8.GetByteCount(text.Substring(0, approximateCharPosition)) > maxContentSize)
            {
                approximateCharPosition--;
            }

            // Try to find a word boundary within the last 100 characters to avoid breaking words
            int wordBoundarySearch = Math.Max(0, approximateCharPosition - 100);
            int lastSpaceIndex = text.LastIndexOf(' ', approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);
            int lastNewlineIndex = text.LastIndexOf('\n', approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);
            int lastPunctuationIndex = text.LastIndexOfAny(new char[] { '.', '!', '?', ';' }, approximateCharPosition - 1, approximateCharPosition - wordBoundarySearch);

            // Use the best break point found
            int breakPoint = Math.Max(Math.Max(lastSpaceIndex, lastNewlineIndex), lastPunctuationIndex);
            if (breakPoint > wordBoundarySearch)
            {
                approximateCharPosition = breakPoint + 1; // Include the break character
            }

            // Final safety check to ensure we don't exceed byte limit
            string truncatedContent = text.Substring(0, approximateCharPosition);
            while (Encoding.UTF8.GetByteCount(truncatedContent + textTruncationNotice) > maxSizeBytes && truncatedContent.Length > 0)
            {
                truncatedContent = truncatedContent.Substring(0, truncatedContent.Length - 1);
            }

            return truncatedContent + textTruncationNotice;
        }

        private void DeleteTempFile(string filePath, string jobId)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted temporary EML file {FilePath} for cancelled job {JobId}", filePath, jobId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temporary EML file {FilePath} for cancelled job {JobId}", filePath, jobId);
            }
        }

        public override void Dispose()
        {
            _cleanupTimer?.Dispose();
            _currentJobCancellation?.Dispose();
            base.Dispose();
        }
    }
}

using MailArchiver.Data;
using MailArchiver.Models;
using MailKit.Net.Imap;
using MailKit;
using MimeKit;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace MailArchiver.Services
{
    public class MBoxImportService : BackgroundService, IMBoxImportService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MBoxImportService> _logger;
        private readonly ConcurrentQueue<MBoxImportJob> _jobQueue = new();
        private readonly ConcurrentDictionary<string, MBoxImportJob> _allJobs = new();
        private readonly Timer _cleanupTimer;
        private CancellationTokenSource? _currentJobCancellation;
        private readonly string _uploadsPath;

        public MBoxImportService(IServiceProvider serviceProvider, ILogger<MBoxImportService> logger, IWebHostEnvironment environment)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _uploadsPath = Path.Combine(environment.ContentRootPath, "uploads", "mbox");

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

        public string QueueImport(MBoxImportJob job)
        {
            job.Status = MBoxImportJobStatus.Queued;
            _allJobs[job.JobId] = job;
            _jobQueue.Enqueue(job);
            _logger.LogInformation("Queued MBox import job {JobId} for file {FileName} ({FileSize} bytes)",
                job.JobId, job.FileName, job.FileSize);
            return job.JobId;
        }

        public MBoxImportJob? GetJob(string jobId)
        {
            return _allJobs.TryGetValue(jobId, out var job) ? job : null;
        }

        public List<MBoxImportJob> GetActiveJobs()
        {
            return _allJobs.Values
                .Where(j => j.Status == MBoxImportJobStatus.Queued || j.Status == MBoxImportJobStatus.Running)
                .OrderBy(j => j.Created)
                .ToList();
        }

        public bool CancelJob(string jobId)
        {
            if (_allJobs.TryGetValue(jobId, out var job))
            {
                if (job.Status == MBoxImportJobStatus.Queued)
                {
                    job.Status = MBoxImportJobStatus.Cancelled;
                    _logger.LogInformation("Cancelled queued MBox import job {JobId}", jobId);
                    return true;
                }
                else if (job.Status == MBoxImportJobStatus.Running)
                {
                    job.Status = MBoxImportJobStatus.Cancelled;
                    _currentJobCancellation?.Cancel();
                    _logger.LogInformation("Requested cancellation of running MBox import job {JobId}", jobId);
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

            _logger.LogInformation("Saved uploaded MBox file to {FilePath}", filePath);
            return filePath;
        }

        public async Task<int> EstimateEmailCountAsync(string filePath)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192);

                int count = 0;
                string? line;

                // Schätze basierend auf "From " Zeilen
                while ((line = await reader.ReadLineAsync()) != null && count < 100000) // Begrenze Schätzung
                {
                    if (line.StartsWith("From ") && line.Contains("@"))
                    {
                        count++;
                    }

                    // Stoppe nach ersten 10MB für Schätzung
                    if (reader.BaseStream.Position > 10_000_000)
                    {
                        var ratio = (double)reader.BaseStream.Length / reader.BaseStream.Position;
                        count = (int)(count * ratio);
                        break;
                    }
                }

                _logger.LogInformation("Estimated {Count} emails in MBox file {FilePath}", count, filePath);
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
                        _logger.LogInformation("Deleted old MBox file {FilePath}", job.FilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old MBox file {FilePath}", job.FilePath);
                }
            }

            if (toRemove.Any())
            {
                _logger.LogInformation("Cleaned up {Count} old MBox import jobs", toRemove.Count);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MBox Import Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_jobQueue.TryDequeue(out var job))
                    {
                        if (job.Status == MBoxImportJobStatus.Cancelled)
                        {
                            _logger.LogInformation("Skipping cancelled MBox import job {JobId}", job.JobId);
                            continue;
                        }

                        await ProcessJob(job, stoppingToken);
                    }
                    else
                    {
                        await Task.Delay(1000, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("MBox Import Background Service stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in MBox Import Background Service");
                    await Task.Delay(5000, stoppingToken);
                }
            }
        }

        private async Task ProcessJob(MBoxImportJob job, CancellationToken stoppingToken)
        {
            _currentJobCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var cancellationToken = _currentJobCancellation.Token;

            try
            {
                job.Status = MBoxImportJobStatus.Running;
                job.Started = DateTime.UtcNow;

                _logger.LogInformation("Starting MBox import job {JobId} for file {FileName}",
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

                // Verarbeite MBox-Datei
                await ProcessMBoxFile(job, targetAccount, cancellationToken);

                if (job.Status != MBoxImportJobStatus.Cancelled)
                {
                    job.Status = MBoxImportJobStatus.Completed;
                    job.Completed = DateTime.UtcNow;
                    _logger.LogInformation("Completed MBox import job {JobId}. Success: {Success}, Failed: {Failed}",
                        job.JobId, job.SuccessCount, job.FailedCount);
                }

                // Lösche die temporäre Datei nach erfolgreichem Import
                try
                {
                    if (File.Exists(job.FilePath))
                    {
                        File.Delete(job.FilePath);
                        _logger.LogInformation("Deleted temporary MBox file {FilePath}", job.FilePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary MBox file {FilePath}", job.FilePath);
                }
            }
            catch (OperationCanceledException)
            {
                job.Status = MBoxImportJobStatus.Cancelled;
                job.Completed = DateTime.UtcNow;
                _logger.LogInformation("MBox import job {JobId} was cancelled", job.JobId);
            }
            catch (Exception ex)
            {
                job.Status = MBoxImportJobStatus.Failed;
                job.Completed = DateTime.UtcNow;
                job.ErrorMessage = ex.Message;
                _logger.LogError(ex, "MBox import job {JobId} failed", job.JobId);
            }
            finally
            {
                _currentJobCancellation?.Dispose();
                _currentJobCancellation = null;
            }
        }

        private async Task ProcessMBoxFile(MBoxImportJob job, MailAccount targetAccount, CancellationToken cancellationToken)
        {
            var stream = new FileStream(job.FilePath, FileMode.Open, FileAccess.Read);
            var parser = new MimeParser(stream, MimeFormat.Mbox);

            try
            {
                while (!parser.IsEndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var message = await parser.ParseMessageAsync(cancellationToken);
                        job.CurrentEmailSubject = message.Subject;
                        job.ProcessedBytes = stream.Position;

                        // Importiere E-Mail in die Datenbank
                        var imported = await ImportEmailToDatabase(message, targetAccount, job);

                        if (imported)
                        {
                            job.SuccessCount++;
                        }
                        else
                        {
                            job.FailedCount++;
                        }

                        job.ProcessedEmails++;

                        // Kleine Pause alle 10 E-Mails
                        if (job.ProcessedEmails % 10 == 0)
                        {
                            await Task.Delay(100, cancellationToken);
                        }

                        // Log Progress alle 100 E-Mails
                        if (job.ProcessedEmails % 100 == 0)
                        {
                            var progressPercent = job.TotalEmails > 0 ? (job.ProcessedEmails * 100.0 / job.TotalEmails) : 0;
                            _logger.LogInformation("Job {JobId}: Processed {Processed} emails ({Progress:F1}%)",
                                job.JobId, job.ProcessedEmails, progressPercent);
                        }
                    }
                    catch (FormatException ex)
                    {
                        _logger.LogWarning(ex, "Job {JobId}: Skipping malformed email at position {Position}",
                            job.JobId, stream.Position);
                        job.FailedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Job {JobId}: Error processing email at position {Position}",
                            job.JobId, stream.Position);
                        job.FailedCount++;
                    }
                }
            }
            finally
            {
                // Stream und Parser explizit freigeben
                parser = null; // Parser hat keinen Dispose
                stream?.Dispose();
            }
        }

        private async Task<bool> ImportEmailToDatabase(MimeMessage message, MailAccount account, MBoxImportJob job)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

                // Erstelle eindeutige Message-ID
                var messageId = message.MessageId ??
                    $"mbox-import-{job.JobId}-{job.ProcessedEmails}-{message.Date.Ticks}";

                // Prüfe ob E-Mail bereits existiert
                var existing = await context.ArchivedEmails
                    .FirstOrDefaultAsync(e => e.MessageId == messageId && e.MailAccountId == account.Id);

                if (existing != null)
                {
                    _logger.LogDebug("Email already exists: {MessageId}", messageId);
                    return false; // Bereits vorhanden, aber kein Fehler
                }

                // Sammle ALLE Anhänge einschließlich inline Images
                var allAttachments = new List<MimePart>();
                CollectAllAttachments(message.Body, allAttachments);

                var archivedEmail = new ArchivedEmail
                {
                    MailAccountId = account.Id,
                    MessageId = messageId,
                    Subject = CleanText(message.Subject ?? "(No Subject)"),
                    From = CleanText(message.From.ToString()),
                    To = CleanText(message.To.ToString()),
                    Cc = CleanText(message.Cc?.ToString() ?? string.Empty),
                    Bcc = CleanText(message.Bcc?.ToString() ?? string.Empty),
                    SentDate = message.Date.UtcDateTime,
                    ReceivedDate = DateTime.UtcNow,
                    IsOutgoing = DetermineIfOutgoing(message, account),
                    HasAttachments = allAttachments.Any(),
                    Body = CleanText(message.TextBody ?? string.Empty),
                    HtmlBody = CleanHtmlForStorage(message.HtmlBody ?? string.Empty),
                    FolderName = job.TargetFolder
                };

                context.ArchivedEmails.Add(archivedEmail);
                await context.SaveChangesAsync();

                // Speichere alle Anhänge
                if (allAttachments.Any())
                {
                    await SaveAllAttachments(context, allAttachments, archivedEmail.Id);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import email to database: {Subject}", message.Subject);
                return false;
            }
        }

        // Hilfsmethoden für MBox Import (kopieren Sie die entsprechenden Methoden aus EmailService)
        private void CollectAllAttachments(MimeEntity entity, List<MimePart> attachments)
        {
            if (entity is MimePart mimePart)
            {
                if (mimePart.IsAttachment || IsInlineContent(mimePart))
                {
                    attachments.Add(mimePart);
                }
            }
            else if (entity is Multipart multipart)
            {
                foreach (var child in multipart)
                {
                    CollectAllAttachments(child, attachments);
                }
            }
            else if (entity is MessagePart messagePart)
            {
                CollectAllAttachments(messagePart.Message.Body, attachments);
            }
        }

        private bool IsInlineContent(MimePart mimePart)
        {
            if (mimePart.ContentDisposition?.Disposition?.Equals("inline", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(mimePart.ContentId) &&
                mimePart.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
            {
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

        private string CleanHtmlForStorage(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            html = html.Replace("\0", "");

            if (html.Length > 1_000_000)
            {
                return "<html><body><p>Diese E-Mail enthält sehr großen HTML-Inhalt, der gekürzt wurde.</p></body></html>";
            }

            return html;
        }

        public override void Dispose()
        {
            _cleanupTimer?.Dispose();
            _currentJobCancellation?.Dispose();
            base.Dispose();
        }
    }
}
using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using MimeKit;
using System.Globalization;

namespace MailArchiver.Services
{
    public class ExportService : BackgroundService, IExportService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ExportService> _logger;
        private readonly BatchOperationOptions _batchOptions;
        private readonly ConcurrentQueue<AccountExportJob> _jobQueue = new();
        private readonly ConcurrentDictionary<string, AccountExportJob> _allJobs = new();
        private readonly Timer _cleanupTimer;
        private CancellationTokenSource? _currentJobCancellation;
        private readonly string _exportsPath;
        private readonly TimeZoneOptions _timeZoneOptions;

        public ExportService(
            IServiceProvider serviceProvider, 
            ILogger<ExportService> logger, 
            IWebHostEnvironment environment, 
            IOptions<BatchOperationOptions> batchOptions,
            IOptions<TimeZoneOptions> timeZoneOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _batchOptions = batchOptions.Value;
            _timeZoneOptions = timeZoneOptions.Value;
            _exportsPath = Path.Combine(environment.ContentRootPath, "exports");

            // Create exports directory if it doesn't exist
            Directory.CreateDirectory(_exportsPath);

            // Cleanup timer: Remove old jobs and files every day
            _cleanupTimer = new Timer(
                callback: _ => CleanupOldJobs(),
                state: null,
                dueTime: TimeSpan.FromHours(24),
                period: TimeSpan.FromHours(24)
            );
        }

        public string QueueExport(int mailAccountId, AccountExportFormat format, string userId)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
            
            var account = context.MailAccounts.Find(mailAccountId);
            if (account == null)
            {
                throw new ArgumentException($"Mail account {mailAccountId} not found");
            }

            var job = new AccountExportJob
            {
                MailAccountId = mailAccountId,
                MailAccountName = account.Name,
                Format = format,
                UserId = userId,
                Status = AccountExportJobStatus.Queued
            };

            _allJobs[job.JobId] = job;
            _jobQueue.Enqueue(job);
            _logger.LogInformation("Queued export job {JobId} for account {AccountName} in {Format} format",
                job.JobId, job.MailAccountName, job.Format);
            return job.JobId;
        }

        public AccountExportJob? GetJob(string jobId)
        {
            return _allJobs.TryGetValue(jobId, out var job) ? job : null;
        }

    public List<AccountExportJob> GetActiveJobs()
    {
        // Return all jobs from the last 24 hours, prioritizing active jobs
        var cutoff = DateTime.UtcNow.AddHours(-24);
        return _allJobs.Values
            .Where(j => j.Created >= cutoff)
            .OrderByDescending(j => j.Status == AccountExportJobStatus.Queued || j.Status == AccountExportJobStatus.Running)
            .ThenByDescending(j => j.Created)
            .ToList();
    }
    
    public List<AccountExportJob> GetAllJobs()
    {
        return _allJobs.Values
            .OrderByDescending(j => j.Status == AccountExportJobStatus.Queued || j.Status == AccountExportJobStatus.Running)
            .ThenByDescending(j => j.Created)
            .ToList();
    }

        public bool CancelJob(string jobId)
        {
            if (_allJobs.TryGetValue(jobId, out var job))
            {
                if (job.Status == AccountExportJobStatus.Queued)
                {
                    job.Status = AccountExportJobStatus.Cancelled;
                    job.Completed = DateTime.UtcNow;
                    _logger.LogInformation("Cancelled queued export job {JobId}", jobId);
                    return true;
                }
                else if (job.Status == AccountExportJobStatus.Running)
                {
                    job.Status = AccountExportJobStatus.Cancelled;
                    _currentJobCancellation?.Cancel();
                    _logger.LogInformation("Requested cancellation of running export job {JobId}", jobId);
                    return true;
                }
            }
            return false;
        }

        public FileResult? GetExportForDownload(string jobId)
        {
            var job = GetJob(jobId);
            if (job == null || job.Status != AccountExportJobStatus.Completed || string.IsNullOrEmpty(job.OutputFilePath))
            {
                return null;
            }

            if (!File.Exists(job.OutputFilePath))
            {
                return null;
            }

            var fileName = $"{job.MailAccountName}_{job.Format}_{job.Created:yyyyMMdd_HHmmss}.zip";

            return new FileResult
            {
                FilePath = job.OutputFilePath,
                FileName = fileName,
                ContentType = "application/zip"
            };
        }

        public bool MarkAsDownloaded(string jobId)
        {
            if (_allJobs.TryGetValue(jobId, out var job))
            {
                job.Status = AccountExportJobStatus.Downloaded;
                job.Completed = DateTime.UtcNow; // Update completion time for cleanup
                
                // Don't delete the file immediately - it's still being streamed to the client
                // The file will be cleaned up automatically by the cleanup job after 7 days
                _logger.LogInformation("Marked export job {JobId} as downloaded. File will be cleaned up automatically.", jobId);
                
                return true;
            }
            return false;
        }

        public void CleanupOldJobs()
        {
            var cutoffTime = DateTime.UtcNow.AddDays(-7); // Remove jobs older than 7 days
            var toRemove = _allJobs.Values
                .Where(j => j.Completed.HasValue && j.Completed < cutoffTime)
                .ToList();

            foreach (var job in toRemove)
            {
                _allJobs.TryRemove(job.JobId, out _);

                // Delete associated file
                if (!string.IsNullOrEmpty(job.OutputFilePath))
                {
                    try
                    {
                        if (File.Exists(job.OutputFilePath))
                        {
                            File.Delete(job.OutputFilePath);
                            _logger.LogInformation("Deleted old export file {FilePath}", job.OutputFilePath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete old export file {FilePath}", job.OutputFilePath);
                    }
                }
            }

            if (toRemove.Any())
            {
                _logger.LogInformation("Cleaned up {Count} old export jobs", toRemove.Count);
            }
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Export Background Service is starting.");
            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Export Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_jobQueue.TryDequeue(out var job))
                    {
                        if (job.Status == AccountExportJobStatus.Cancelled)
                        {
                            _logger.LogInformation("Skipping cancelled export job {JobId}", job.JobId);
                            continue;
                        }

                        await ProcessJob(job, stoppingToken);
                    }
                    else
                    {
                        await Task.Delay(100, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Export Background Service stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Export Background Service");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Export Background Service is stopping.");
            return base.StopAsync(cancellationToken);
        }

        private async Task ProcessJob(AccountExportJob job, CancellationToken stoppingToken)
        {
            _currentJobCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var cancellationToken = _currentJobCancellation.Token;

            try
            {
                job.Status = AccountExportJobStatus.Running;
                job.Started = DateTime.UtcNow;

                _logger.LogInformation("Starting export job {JobId} for account {AccountName} in {Format} format",
                    job.JobId, job.MailAccountName, job.Format);

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

                // Get email count
                job.TotalEmails = await context.ArchivedEmails
                    .CountAsync(e => e.MailAccountId == job.MailAccountId, cancellationToken);

                if (job.TotalEmails == 0)
                {
                    throw new InvalidOperationException("No emails found for export");
                }

                // Count incoming and outgoing emails
                job.IncomingEmailsCount = await context.ArchivedEmails
                    .CountAsync(e => e.MailAccountId == job.MailAccountId && !e.IsOutgoing, cancellationToken);
                job.OutgoingEmailsCount = await context.ArchivedEmails
                    .CountAsync(e => e.MailAccountId == job.MailAccountId && e.IsOutgoing, cancellationToken);

                // Generate output file path
                var fileName = $"export_{job.JobId}_{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
                job.OutputFilePath = Path.Combine(_exportsPath, fileName);

                // Perform export based on format
                if (job.Format == AccountExportFormat.EML)
                {
                    await ExportToEmlFormat(job, context, cancellationToken);
                }
                else if (job.Format == AccountExportFormat.MBox)
                {
                    await ExportToMBoxFormat(job, context, cancellationToken);
                }

                if (job.Status != AccountExportJobStatus.Cancelled)
                {
                    job.Status = AccountExportJobStatus.Completed;
                    job.Completed = DateTime.UtcNow;
                    
                    // Get file size
                    if (File.Exists(job.OutputFilePath))
                    {
                        job.OutputFileSize = new FileInfo(job.OutputFilePath).Length;
                    }

                    _logger.LogInformation("Completed export job {JobId}. Processed: {Processed} emails, File size: {Size} bytes",
                        job.JobId, job.ProcessedEmails, job.OutputFileSize);
                }
            }
            catch (OperationCanceledException)
            {
                job.Status = AccountExportJobStatus.Cancelled;
                job.Completed = DateTime.UtcNow;
                
                // Delete partial export file
                if (!string.IsNullOrEmpty(job.OutputFilePath) && File.Exists(job.OutputFilePath))
                {
                    try
                    {
                        File.Delete(job.OutputFilePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete partial export file {FilePath}", job.OutputFilePath);
                    }
                }
                
                _logger.LogInformation("Export job {JobId} was cancelled", job.JobId);
            }
            catch (Exception ex)
            {
                job.Status = AccountExportJobStatus.Failed;
                job.Completed = DateTime.UtcNow;
                job.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Export job {JobId} failed", job.JobId);
            }
            finally
            {
                _currentJobCancellation?.Dispose();
                _currentJobCancellation = null;
            }
        }

        private async Task ExportToEmlFormat(AccountExportJob job, MailArchiverDbContext context, CancellationToken cancellationToken)
        {
            using var zipStream = new FileStream(job.OutputFilePath, FileMode.Create, FileAccess.Write);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            // Get distinct folder names for this account
            var folderNames = await context.ArchivedEmails
                .Where(e => e.MailAccountId == job.MailAccountId)
                .Select(e => e.FolderName)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Process emails for each folder
            foreach (var folderName in folderNames)
            {
                await ProcessEmailsForEmlExportByFolder(job, context, archive, folderName, cancellationToken);
            }
        }

        private async Task ProcessEmailsForEmlExportByFolder(AccountExportJob job, MailArchiverDbContext context, ZipArchive archive, string folderName, CancellationToken cancellationToken)
        {
            var emails = context.ArchivedEmails
                .Where(e => e.MailAccountId == job.MailAccountId && e.FolderName == folderName)
                .Include(e => e.Attachments)
                .AsAsyncEnumerable();

            var emailIndex = 1;
            await foreach (var email in emails.WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    job.CurrentEmailSubject = email.Subject;

                    // Create MIME message from archived email
                    var mimeMessage = await CreateMimeMessageFromArchived(email);

                    // Generate safe filename with In/Out indicator
                    var safeSubject = SanitizeFileName(email.Subject);
                    var inOutIndicator = email.IsOutgoing ? "Out" : "In";
                    var fileName = $"{emailIndex:D6}_{email.SentDate:yyyyMMdd_HHmmss}_{inOutIndicator}_{safeSubject}.eml";
                    var entryName = $"{SanitizeFileName(folderName)}/{fileName}";

                    // Add to ZIP archive
                    var entry = archive.CreateEntry(entryName);
                    using var entryStream = entry.Open();
                    await mimeMessage.WriteToAsync(entryStream, cancellationToken);

                    job.ProcessedEmails++;
                    emailIndex++;

                    // Small pause every 10 emails
                    if (job.ProcessedEmails % 10 == 0 && _batchOptions.PauseBetweenEmailsMs > 0)
                    {
                        await Task.Delay(_batchOptions.PauseBetweenEmailsMs, cancellationToken);
                    }

                    // Log progress every 100 emails
                    if (job.ProcessedEmails % 100 == 0)
                    {
                        var progressPercent = job.TotalEmails > 0 ? (job.ProcessedEmails * 100.0 / job.TotalEmails) : 0;
                        _logger.LogInformation("Job {JobId}: Processed {Processed} emails ({Progress:F1}%)",
                            job.JobId, job.ProcessedEmails, progressPercent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Job {JobId}: Failed to export email {EmailId}: {Subject}", 
                        job.JobId, email.Id, email.Subject);
                }
            }
        }

        private async Task ExportToMBoxFormat(AccountExportJob job, MailArchiverDbContext context, CancellationToken cancellationToken)
        {
            using var zipStream = new FileStream(job.OutputFilePath, FileMode.Create, FileAccess.Write);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            // Get distinct folder names for this account
            var folderNames = await context.ArchivedEmails
                .Where(e => e.MailAccountId == job.MailAccountId)
                .Select(e => e.FolderName)
                .Distinct()
                .ToListAsync(cancellationToken);

            // Process emails for each folder
            foreach (var folderName in folderNames)
            {
                var mboxEntry = archive.CreateEntry($"{SanitizeFileName(folderName)}.mbox");
                using var mboxStream = mboxEntry.Open();
                await ProcessEmailsForMBoxExportByFolder(job, context, mboxStream, folderName, cancellationToken);
            }
        }

        private async Task ProcessEmailsForMBoxExportByFolder(AccountExportJob job, MailArchiverDbContext context, Stream mboxStream, string folderName, CancellationToken cancellationToken)
        {
            using var writer = new StreamWriter(mboxStream, Encoding.UTF8, leaveOpen: true);

            var emails = context.ArchivedEmails
                .Where(e => e.MailAccountId == job.MailAccountId && e.FolderName == folderName)
                .Include(e => e.Attachments)
                .OrderBy(e => e.SentDate)
                .AsAsyncEnumerable();

            await foreach (var email in emails.WithCancellation(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    job.CurrentEmailSubject = email.Subject;

                    // Write mbox separator line
                    var fromLine = CreateMBoxFromLine(email);
                    await writer.WriteLineAsync(fromLine);

                    // Create MIME message and write to mbox
                    var mimeMessage = await CreateMimeMessageFromArchived(email);
                    
                    using var messageStream = new MemoryStream();
                    await mimeMessage.WriteToAsync(messageStream, cancellationToken);
                    messageStream.Position = 0;

                    using var messageReader = new StreamReader(messageStream, Encoding.UTF8);
                    string? line;
                    while ((line = await messageReader.ReadLineAsync()) != null)
                    {
                        // Escape lines that start with "From " (mbox format requirement)
                        if (line.StartsWith("From "))
                        {
                            line = ">" + line;
                        }
                        await writer.WriteLineAsync(line);
                    }

                    // Add empty line after message
                    await writer.WriteLineAsync();

                    job.ProcessedEmails++;

                    // Small pause every 10 emails
                    if (job.ProcessedEmails % 10 == 0 && _batchOptions.PauseBetweenEmailsMs > 0)
                    {
                        await Task.Delay(_batchOptions.PauseBetweenEmailsMs, cancellationToken);
                    }

                    // Log progress every 100 emails
                    if (job.ProcessedEmails % 100 == 0)
                    {
                        var progressPercent = job.TotalEmails > 0 ? (job.ProcessedEmails * 100.0 / job.TotalEmails) : 0;
                        _logger.LogInformation("Job {JobId}: Processed {Processed} emails ({Progress:F1}%)",
                            job.JobId, job.ProcessedEmails, progressPercent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Job {JobId}: Failed to export email {EmailId}: {Subject}", 
                        job.JobId, email.Id, email.Subject);
                }
            }

            await writer.FlushAsync();
        }

        private async Task<MimeMessage> CreateMimeMessageFromArchived(ArchivedEmail email)
        {
            var message = new MimeMessage();

            // Set headers
            message.MessageId = email.MessageId;
            message.Subject = email.Subject;
            
            // The SentDate in the database is already in the configured display timezone (converted during sync)
            // We need to specify this timezone when creating the DateTimeOffset to preserve the correct time
            var displayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(_timeZoneOptions.DisplayTimeZoneId);
            message.Date = new DateTimeOffset(email.SentDate, displayTimeZone.GetUtcOffset(email.SentDate));

            // Parse addresses
            if (!string.IsNullOrEmpty(email.From))
            {
                try
                {
                    message.From.AddRange(InternetAddressList.Parse(email.From));
                }
                catch
                {
                    message.From.Add(new MailboxAddress("", email.From));
                }
            }

            if (!string.IsNullOrEmpty(email.To))
            {
                try
                {
                    message.To.AddRange(InternetAddressList.Parse(email.To));
                }
                catch
                {
                    message.To.Add(new MailboxAddress("", email.To));
                }
            }

            if (!string.IsNullOrEmpty(email.Cc))
            {
                try
                {
                    message.Cc.AddRange(InternetAddressList.Parse(email.Cc));
                }
                catch
                {
                    message.Cc.Add(new MailboxAddress("", email.Cc));
                }
            }

            if (!string.IsNullOrEmpty(email.Bcc))
            {
                try
                {
                    message.Bcc.AddRange(InternetAddressList.Parse(email.Bcc));
                }
                catch
                {
                    message.Bcc.Add(new MailboxAddress("", email.Bcc));
                }
            }

            // Create body - use untruncated versions if available for compliance
            var bodyBuilder = new BodyBuilder();

            // Use BodyUntruncatedText if available, otherwise fall back to Body
            var textBody = !string.IsNullOrEmpty(email.BodyUntruncatedText) 
                ? email.BodyUntruncatedText 
                : email.Body;

            if (!string.IsNullOrEmpty(textBody))
            {
                bodyBuilder.TextBody = textBody;
            }

            // Use BodyUntruncatedHtml if available, otherwise fall back to HtmlBody
            var htmlBody = !string.IsNullOrEmpty(email.BodyUntruncatedHtml) 
                ? email.BodyUntruncatedHtml 
                : email.HtmlBody;

            if (!string.IsNullOrEmpty(htmlBody))
            {
                bodyBuilder.HtmlBody = htmlBody;
            }

            // Add attachments
            if (email.Attachments?.Any() == true)
            {
                // Separate inline attachments from regular attachments
                var inlineAttachments = email.Attachments.Where(a => !string.IsNullOrEmpty(a.ContentId)).ToList();
                var regularAttachments = email.Attachments.Where(a => string.IsNullOrEmpty(a.ContentId)).ToList();
                
                // Add inline attachments first so they can be referenced in the HTML body
                foreach (var attachment in inlineAttachments)
                {
                    try
                    {
                        // Extract just the filename in case attachment.FileName contains a path or URI
                        var fileName = Path.GetFileName(attachment.FileName);
                        if (string.IsNullOrEmpty(fileName))
                            fileName = "attachment";
                        
                        var contentType = ContentType.Parse(attachment.ContentType);
                        var mimePart = bodyBuilder.LinkedResources.Add(fileName, attachment.Content, contentType);
                        mimePart.ContentId = attachment.ContentId;
                    }
                    catch
                    {
                        // Fallback for invalid content types
                        var fileName = Path.GetFileName(attachment.FileName);
                        if (string.IsNullOrEmpty(fileName))
                            fileName = "attachment";
                        
                        var mimePart = bodyBuilder.LinkedResources.Add(fileName, attachment.Content);
                        mimePart.ContentId = attachment.ContentId;
                    }
                }
                
                // Add regular attachments
                foreach (var attachment in regularAttachments)
                {
                    try
                    {
                        // Extract just the filename in case attachment.FileName contains a path or URI
                        var fileName = Path.GetFileName(attachment.FileName);
                        if (string.IsNullOrEmpty(fileName))
                            fileName = "attachment";
                        
                        var contentType = ContentType.Parse(attachment.ContentType);
                        bodyBuilder.Attachments.Add(fileName, attachment.Content, contentType);
                    }
                    catch
                    {
                        // Fallback for invalid content types
                        var fileName = Path.GetFileName(attachment.FileName);
                        if (string.IsNullOrEmpty(fileName))
                            fileName = "attachment";
                        
                        bodyBuilder.Attachments.Add(fileName, attachment.Content);
                    }
                }
            }

            message.Body = bodyBuilder.ToMessageBody();
            return message;
        }


        private string CreateMBoxFromLine(ArchivedEmail email)
        {
            var fromAddress = "unknown@example.com";
            if (!string.IsNullOrEmpty(email.From))
            {
                try
                {
                    var addresses = InternetAddressList.Parse(email.From);
                    if (addresses.Mailboxes.Any())
                    {
                        fromAddress = addresses.Mailboxes.First().Address;
                    }
                }
                catch
                {
                    fromAddress = email.From.Contains("@") ? email.From : "unknown@example.com";
                }
            }

            // The SentDate in the database is already in the configured display timezone (converted during sync)
            // Format: "From address@domain.com Mon Jan 01 12:00:00 2024"
            var dateString = email.SentDate.ToString("ddd MMM dd HH:mm:ss yyyy", CultureInfo.InvariantCulture);
            return $"From {fromAddress} {dateString}";
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "untitled";

            // Remove invalid characters
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new StringBuilder();

            foreach (var c in fileName)
            {
                if (invalidChars.Contains(c))
                {
                    sanitized.Append('_');
                }
                else
                {
                    sanitized.Append(c);
                }
            }

            var result = sanitized.ToString().Trim();
            if (result.Length > 50)
            {
                result = result.Substring(0, 50);
            }

            return string.IsNullOrEmpty(result) ? "untitled" : result;
        }

        public override void Dispose()
        {
            _cleanupTimer?.Dispose();
            _currentJobCancellation?.Dispose();
            base.Dispose();
        }
    }
}

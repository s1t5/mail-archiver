using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services
{
    /// <summary>
    /// Background service that builds audit data export packages
    /// (index DTD + INDEX.XML + CSV tables) from the existing archive.
    /// The job lifecycle is persisted in the AuditExportJobs table so the
    /// history shown on the audit export page is revision-safe; every run is
    /// additionally recorded in the AccessLogs table.
    /// </summary>
    public class AuditExportService : BackgroundService, IAuditExportService
    {
        private const string EmailCsvName = "emails.csv";
        private const string AttachmentCsvName = "attachments.csv";
        private const string IndexXmlName = "INDEX.XML";
        private const string DtdName = "index.dtd";
        private const int BatchSize = 1000;

        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AuditExportService> _logger;
        private readonly AuditExportOptions _options;
        private readonly ConcurrentQueue<Guid> _jobQueue = new();
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _completionSignals = new();
        private readonly Timer _cleanupTimer;
        private CancellationTokenSource? _currentJobCancellation;
        private readonly string _outputPath;

        public AuditExportService(
            IServiceProvider serviceProvider,
            ILogger<AuditExportService> logger,
            IWebHostEnvironment environment,
            IOptions<AuditExportOptions> options)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = options.Value;
            _outputPath = Path.IsPathRooted(_options.OutputDirectory)
                ? _options.OutputDirectory
                : Path.Combine(environment.ContentRootPath, _options.OutputDirectory);

            Directory.CreateDirectory(_outputPath);

            // Cleanup timer: delete stale export files every day
            _cleanupTimer = new Timer(
                callback: _ => CleanupOldExportFiles(),
                state: null,
                dueTime: TimeSpan.FromHours(24),
                period: TimeSpan.FromHours(24)
            );
        }

        public async Task<AuditExportJob> StartJobAsync(AuditExportRequest request, string username)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

            string? mailAccountName = null;
            if (request.MailAccountId.HasValue)
            {
                mailAccountName = await context.MailAccounts
                    .Where(a => a.Id == request.MailAccountId.Value)
                    .Select(a => a.Name)
                    .FirstOrDefaultAsync();
            }

            var job = new AuditExportJob
            {
                Id = Guid.NewGuid(),
                Username = username,
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                MailAccountId = request.MailAccountId,
                MailAccountName = mailAccountName,
                IncludeAttachments = request.IncludeAttachments,
                DataSupplierName = request.DataSupplierName,
                DataSupplierLocation = request.DataSupplierLocation,
                DataSupplierComment = request.DataSupplierComment,
                Status = AuditExportJobStatus.Queued,
                Created = DateTime.UtcNow
            };

            context.AuditExportJobs.Add(job);
            await context.SaveChangesAsync();

            _completionSignals[job.Id] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _jobQueue.Enqueue(job.Id);
            _logger.LogInformation("Queued audit data export job {JobId} for user {Username}", job.Id, username);
            return job;
        }

        /// <summary>
        /// Waits until the given job has reached a final state (or the timeout elapses).
        /// Primarily used by integration tests; signals live in memory only, so after an
        /// application restart there is nothing to wait for and the method returns immediately.
        /// </summary>
        public async Task WaitForJobAsync(Guid jobId, TimeSpan timeout)
        {
            if (_completionSignals.TryGetValue(jobId, out var tcs))
            {
                var completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
                if (completed != tcs.Task)
                {
                    throw new TimeoutException($"Audit data export job {jobId} did not finish within {timeout}.");
                }
            }
        }

        public async Task<AuditExportJob?> GetJobAsync(Guid jobId)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
            return await context.AuditExportJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId);
        }

        public async Task<List<AuditExportJob>> GetRecentJobsAsync(int limit = 20)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
            return await context.AuditExportJobs.AsNoTracking()
                .OrderByDescending(j => j.Created)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<bool> CancelJobAsync(Guid jobId)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
            var job = await context.AuditExportJobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null)
            {
                return false;
            }

            if (job.Status == AuditExportJobStatus.Queued)
            {
                job.Status = AuditExportJobStatus.Cancelled;
                job.Completed = DateTime.UtcNow;
                await context.SaveChangesAsync();
                _logger.LogInformation("Cancelled queued audit data export job {JobId}", jobId);
                return true;
            }

            if (job.Status == AuditExportJobStatus.Running)
            {
                // The running loop observes the cancellation token and persists the final state
                _currentJobCancellation?.Cancel();
                _logger.LogInformation("Requested cancellation of running audit data export job {JobId}", jobId);
                return true;
            }

            return false;
        }

        public async Task<AuditExportFileResult?> GetExportForDownloadAsync(Guid jobId)
        {
            var job = await GetJobAsync(jobId);
            if (job == null ||
                (job.Status != AuditExportJobStatus.Completed && job.Status != AuditExportJobStatus.Downloaded) ||
                string.IsNullOrEmpty(job.OutputFilePath) ||
                !File.Exists(job.OutputFilePath))
            {
                return null;
            }

            return new AuditExportFileResult
            {
                FilePath = job.OutputFilePath,
                FileName = $"audit-export-{job.Created:yyyyMMdd_HHmmss}.zip",
                ContentType = "application/zip"
            };
        }

        public async Task MarkAsDownloadedAsync(Guid jobId)
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
            var job = await context.AuditExportJobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job != null && job.Status == AuditExportJobStatus.Completed)
            {
                job.Status = AuditExportJobStatus.Downloaded;
                await context.SaveChangesAsync();
            }
        }

        public void CleanupOldExportFiles()
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);
                if (!Directory.Exists(_outputPath))
                {
                    return;
                }

                foreach (var file in Directory.EnumerateFiles(_outputPath, "audit-export-*.zip"))
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                    {
                        try { File.Delete(file); }
                        catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete stale audit export file {File}", file); }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit export file cleanup failed");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Audit data export background service started");

            // Best-effort startup cleanup (retention window)
            try
            {
                CleanupOldExportFiles();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit export startup cleanup failed");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_jobQueue.TryDequeue(out var jobId))
                    {
                        await ProcessJob(jobId, stoppingToken);
                    }
                    else
                    {
                        await Task.Delay(100, stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled error in audit data export service loop");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        private async Task ProcessJob(Guid jobId, CancellationToken stoppingToken)
        {
            using var jobScope = _serviceProvider.CreateScope();
            var context = jobScope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
            var accessLogService = jobScope.ServiceProvider.GetRequiredService<IAccessLogService>();
            var timeZoneOptions = jobScope.ServiceProvider.GetRequiredService<IOptions<TimeZoneOptions>>();
            var dateTimeHelper = new DateTimeHelper(timeZoneOptions);

            var job = await context.AuditExportJobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null)
            {
                _logger.LogWarning("Audit data export job {JobId} not found in database", jobId);
                return;
            }

            if (job.Status != AuditExportJobStatus.Queued)
            {
                return;
            }

            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _currentJobCancellation = cancellation;

            var outputFilePath = Path.Combine(_outputPath, $"audit-export-{job.Created:yyyyMMdd_HHmmss}-{job.Id.ToString("N")[..8]}.zip");

            try
            {
                job.Status = AuditExportJobStatus.Running;
                job.Started = DateTime.UtcNow;
                job.OutputFilePath = outputFilePath;
                await context.SaveChangesAsync();

                var query = BuildEmailQuery(context, job);
                job.TotalEmails = await query.CountAsync(cancellation.Token);
                await context.SaveChangesAsync(cancellation.Token);

                using var zipStream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
                using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

                await WriteDtdEntry(archive);
                await WriteEmailsCsvAsync(job, context, dateTimeHelper, archive, cancellation.Token);
                if (job.IncludeAttachments)
                {
                    await WriteAttachmentsCsvAsync(job, context, archive, cancellation.Token);
                }
                await WriteIndexXmlAsync(job, archive);

                job.Status = AuditExportJobStatus.Completed;
                job.Completed = DateTime.UtcNow;
                job.OutputFileSize = new FileInfo(outputFilePath).Length;
                await context.SaveChangesAsync();

                await WriteCompletionLogAsync(accessLogService, job, result: null);
                _logger.LogInformation("Audit data export job {JobId} completed: {TotalEmails} emails, {Size} bytes",
                    job.Id, job.TotalEmails, job.OutputFileSize);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested || stoppingToken.IsCancellationRequested)
            {
                job.Status = AuditExportJobStatus.Cancelled;
                job.Completed = DateTime.UtcNow;
                await TrySaveAsync(context);
                TryDeleteFile(outputFilePath);
                await WriteCompletionLogAsync(accessLogService, job, "cancelled");
                _logger.LogInformation("Audit data export job {JobId} cancelled", job.Id);
            }
            catch (Exception ex)
            {
                job.Status = AuditExportJobStatus.Cancelled == job.Status ? job.Status : AuditExportJobStatus.Failed;
                job.ErrorMessage = Truncate(ex.Message, 2000);
                if (job.Status == AuditExportJobStatus.Failed)
                {
                    job.Completed = DateTime.UtcNow;
                }
                await TrySaveAsync(context);
                TryDeleteFile(outputFilePath);
                await WriteCompletionLogAsync(accessLogService, job, $"failed: {ex.Message}");
                _logger.LogError(ex, "Audit data export job {JobId} failed", job.Id);
            }
            finally
            {
                _currentJobCancellation = null;
                if (_completionSignals.TryRemove(jobId, out var tcs))
                {
                    tcs.TrySetResult();
                }
            }
        }

        private async Task TrySaveAsync(MailArchiverDbContext context)
        {
            try
            {
                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist audit data export job final state");
            }
        }

        private async Task WriteCompletionLogAsync(IAccessLogService accessLogService, AuditExportJob job, string? result)
        {
            try
            {
                var details = new
                {
                    jobId = job.Id,
                    period = $"{job.FromDate:yyyy-MM-dd} - {job.ToDate:yyyy-MM-dd}",
                    mailbox = job.MailAccountId.HasValue ? (job.MailAccountName ?? job.MailAccountId.ToString()) : "*",
                    emails = job.TotalEmails,
                    includeAttachments = job.IncludeAttachments,
                    result = result ?? "completed",
                    sizeBytes = job.OutputFileSize
                };
                await accessLogService.LogAccessAsync(
                    job.Username,
                    AccessLogType.AuditExport,
                    searchParameters: JsonSerializer.Serialize(details),
                    mailAccountId: job.MailAccountId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write completion access log for audit export job {JobId}", job.Id);
            }
        }

        private static string Truncate(string value, int maxLength)
            => string.IsNullOrEmpty(value) ? value : (value.Length <= maxLength ? value : value.Substring(0, maxLength));

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Best effort
            }
        }

        private static IQueryable<ArchivedEmail> BuildEmailQuery(MailArchiverDbContext context, AuditExportJob job)
        {
            var query = context.ArchivedEmails.AsNoTracking();
            if (job.MailAccountId.HasValue)
            {
                query = query.Where(e => e.MailAccountId == job.MailAccountId.Value);
            }
            query = query.Where(e => e.SentDate >= job.FromDate && e.SentDate <= job.ToDate);
            return query.OrderBy(e => e.Id);
        }

        private async Task WriteDtdEntry(ZipArchive archive)
        {
            var assembly = typeof(AuditExportService).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("index.dtd", StringComparison.OrdinalIgnoreCase));
            if (resourceName == null)
            {
                throw new InvalidOperationException("Embedded index DTD resource not found");
            }

            await using var stream = assembly.GetManifestResourceStream(resourceName)!;
            var entry = archive.CreateEntry(DtdName, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await stream.CopyToAsync(entryStream);
        }

        private async Task WriteEmailsCsvAsync(AuditExportJob job, MailArchiverDbContext context, DateTimeHelper dateTimeHelper, ZipArchive archive, CancellationToken cancellationToken)
        {
            var entry = archive.CreateEntry(EmailCsvName, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));

            var lastId = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var query = BuildEmailQuery(context, job);
                var batch = await query
                    .Where(e => e.Id > lastId)
                    .Select(e => new
                    {
                        e.Id,
                        e.MessageId,
                        e.SentDate,
                        e.ReceivedDate,
                        e.IsOutgoing,
                        e.From,
                        e.To,
                        e.Cc,
                        e.Bcc,
                        e.Subject,
                        e.FolderName,
                        e.HasAttachments,
                        AttachmentCount = e.Attachments.Count,
                        AccountEmail = context.MailAccounts.Where(a => a.Id == e.MailAccountId).Select(a => a.EmailAddress).FirstOrDefault() ?? string.Empty
                    })
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);

                if (batch.Count == 0)
                {
                    break;
                }

                foreach (var email in batch)
                {
                    var line = string.Join(';',
                        Csv(email.Id.ToString()),
                        Csv(email.MessageId),
                        Csv(ToIso8601Utc(email.SentDate, dateTimeHelper)),
                        Csv(ToIso8601Utc(email.ReceivedDate, dateTimeHelper)),
                        Csv(email.IsOutgoing ? "1" : "0"),
                        Csv(email.From),
                        Csv(email.To),
                        Csv(email.Cc),
                        Csv(email.Bcc),
                        Csv(email.Subject),
                        Csv(email.FolderName),
                        Csv(email.HasAttachments ? "1" : "0"),
                        Csv(email.AttachmentCount.ToString()),
                        Csv(email.AccountEmail));
                    await writer.WriteAsync(line);
                    await writer.WriteAsync('\n');
                    job.ProcessedEmails++;

                    lastId = email.Id;
                }

                // Persist progress so the polling UI sees movement even for huge ranges
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task WriteAttachmentsCsvAsync(AuditExportJob job, MailArchiverDbContext context, ZipArchive archive, CancellationToken cancellationToken)
        {
            var entry = archive.CreateEntry(AttachmentCsvName, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));

            var emailIds = await BuildEmailQuery(context, job).Select(e => e.Id).ToListAsync(cancellationToken);
            for (var offset = 0; offset < emailIds.Count; offset += BatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var idBatch = emailIds.Skip(offset).Take(BatchSize).ToList();
                var attachments = await context.EmailAttachments.AsNoTracking()
                    .Where(a => idBatch.Contains(a.ArchivedEmailId))
                    .Select(a => new
                    {
                        a.ArchivedEmailId,
                        a.FileName,
                        a.ContentType,
                        a.Size,
                        Hash = a.AttachmentContent != null ? a.AttachmentContent.Hash : null
                    })
                    .OrderBy(a => a.ArchivedEmailId)
                    .ToListAsync(cancellationToken);

                foreach (var attachment in attachments)
                {
                    var line = string.Join(';',
                        Csv(attachment.ArchivedEmailId.ToString()),
                        Csv(attachment.FileName),
                        Csv(attachment.ContentType),
                        Csv(attachment.Size.ToString()),
                        Csv(attachment.Hash ?? string.Empty));
                    await writer.WriteAsync(line);
                    await writer.WriteAsync('\n');
                }
            }
        }

        private async Task WriteIndexXmlAsync(AuditExportJob job, ZipArchive archive)
        {
            var entry = archive.CreateEntry(IndexXmlName, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();

            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = true,
                NewLineChars = "\n"
            };

            using var writer = XmlWriter.Create(entryStream, settings);
            writer.WriteDocType("DataSet", null, DtdName, null);
            writer.WriteStartElement("DataSet");
            writer.WriteElementString("Version", "1.0");

            writer.WriteStartElement("DataSupplier");
            writer.WriteElementString("Name", FirstNonEmpty(job.DataSupplierName, _options.DataSupplierName, "Mail-Archiver"));
            writer.WriteElementString("Location", FirstNonEmpty(job.DataSupplierLocation, _options.DataSupplierLocation));
            var period = $"{job.FromDate:dd.MM.yyyy} - {job.ToDate:dd.MM.yyyy}";
            var comment = FirstNonEmpty(job.DataSupplierComment, _options.Comment);
            if (string.IsNullOrEmpty(comment))
            {
                comment = $"E-Mail-Archiv Mail-Archiver, Zeitraum {period}";
            }
            writer.WriteElementString("Comment", comment);
            writer.WriteEndElement(); // DataSupplier

            writer.WriteStartElement("Media");
            writer.WriteElementString("Name", "E-Mail-Archiv");

            WriteTable(writer, EmailCsvName, "E-Mail-Metadaten", "Metadaten archivierter E-Mails", EmailCsvColumns);
            if (job.IncludeAttachments)
            {
                WriteTable(writer, AttachmentCsvName, "Anhang-Metadaten", "Metadaten archivierter E-Mail-Anhänge", AttachmentCsvColumns);
            }

            writer.WriteEndElement(); // Media
            writer.WriteEndElement(); // DataSet
            writer.Flush();
        }

        private static readonly (string Name, string Type)[] EmailCsvColumns =
        {
            ("Id", "Numeric"),
            ("MessageId", "AlphaNumeric"),
            ("SentDate", "AlphaNumeric"),
            ("ReceivedDate", "AlphaNumeric"),
            ("IsOutgoing", "AlphaNumeric"),
            ("From", "AlphaNumeric"),
            ("To", "AlphaNumeric"),
            ("Cc", "AlphaNumeric"),
            ("Bcc", "AlphaNumeric"),
            ("Subject", "AlphaNumeric"),
            ("FolderName", "AlphaNumeric"),
            ("HasAttachments", "AlphaNumeric"),
            ("AttachmentCount", "Numeric"),
            ("AccountEmail", "AlphaNumeric")
        };

        private static readonly (string Name, string Type)[] AttachmentCsvColumns =
        {
            ("EmailId", "Numeric"),
            ("FileName", "AlphaNumeric"),
            ("ContentType", "AlphaNumeric"),
            ("Size", "Numeric"),
            ("Sha256", "AlphaNumeric")
        };

        private static void WriteTable(XmlWriter writer, string url, string name, string description, (string Name, string Type)[] columns)
        {
            writer.WriteStartElement("Table");
            writer.WriteElementString("URL", url);
            writer.WriteElementString("Name", name);
            writer.WriteElementString("Description", description);
            writer.WriteElementString("UTF8", null);

            writer.WriteStartElement("VariableLength");
            writer.WriteElementString("ColumnDelimiter", ";");
            writer.WriteStartElement("RecordDelimiter");
            writer.WriteCharEntity('\n');
            writer.WriteEndElement();
            writer.WriteElementString("TextEncapsulator", "\"");

            foreach (var (columnName, columnType) in columns)
            {
                writer.WriteStartElement("VariableColumn");
                writer.WriteElementString("Name", columnName);
                if (string.Equals(columnType, "Numeric", StringComparison.OrdinalIgnoreCase))
                {
                    writer.WriteElementString("Numeric", null);
                }
                else
                {
                    writer.WriteElementString("AlphaNumeric", null);
                }
                writer.WriteEndElement(); // VariableColumn
            }

            writer.WriteEndElement(); // VariableLength
            writer.WriteEndElement(); // Table
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            return string.Empty;
        }

        internal static string ToIso8601Utc(DateTime displayDateTime, DateTimeHelper dateTimeHelper)
        {
            var utc = dateTimeHelper.ConvertFromDisplayTimeZoneToUtc(displayDateTime);
            return utc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }

        internal static string Csv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            var escaped = value.Replace("\"", "\"\"");
            var needsQuotes = escaped.Contains(';') || escaped.Contains('"') || escaped.Contains('\n') || escaped.Contains('\r');
            return needsQuotes ? $"\"{escaped}\"" : escaped;
        }
    }
}
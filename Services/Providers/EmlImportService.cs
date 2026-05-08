using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Providers.Eml;
using MailArchiver.Services.Shared;
using MailArchiver.Utilities;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Collections.Concurrent;
using System.IO.Compression;

namespace MailArchiver.Services.Providers
{
    /// <summary>
    /// BackgroundService that processes EML import jobs from a queue.
    /// Delegates email parsing, cleaning, and importing to specialized services.
    /// </summary>
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

        public EmlImportService(IServiceProvider serviceProvider, ILogger<EmlImportService> logger,
            IWebHostEnvironment environment, IOptions<BatchOperationOptions> batchOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _batchOptions = batchOptions.Value;
            _uploadsPath = Path.Combine(environment.ContentRootPath, "uploads", "eml");
            Directory.CreateDirectory(_uploadsPath);
            _cleanupTimer = new Timer(_ => CleanupOldJobs(), null, TimeSpan.FromHours(24), TimeSpan.FromHours(24));
        }

        // ========================================
        // IEmlImportService
        // ========================================

        public string QueueImport(EmlImportJob job)
        {
            job.Status = EmlImportJobStatus.Queued;
            _allJobs[job.JobId] = job;
            _jobQueue.Enqueue(job);
            _logger.LogInformation("Queued EML import job {JobId} for {FileName}", job.JobId, job.FileName);
            return job.JobId;
        }

        public EmlImportJob? GetJob(string jobId)
            => _allJobs.TryGetValue(jobId, out var job) ? job : null;

        public List<EmlImportJob> GetActiveJobs()
            => _allJobs.Values.Where(j => j.Status == EmlImportJobStatus.Queued || j.Status == EmlImportJobStatus.Running)
                .OrderBy(j => j.Created).ToList();

        public List<EmlImportJob> GetAllJobs()
            => _allJobs.Values.OrderByDescending(j => j.Status == EmlImportJobStatus.Running || j.Status == EmlImportJobStatus.Queued)
                .ThenByDescending(j => j.Created).ToList();

        public bool CancelJob(string jobId)
        {
            if (!_allJobs.TryGetValue(jobId, out var job)) return false;
            if (job.Status == EmlImportJobStatus.Queued)
            {
                job.Status = EmlImportJobStatus.Cancelled;
                job.Completed = DateTime.UtcNow;
                DeleteTempFile(job.FilePath, jobId);
                return true;
            }
            if (job.Status == EmlImportJobStatus.Running)
            {
                job.Status = EmlImportJobStatus.Cancelled;
                _currentJobCancellation?.Cancel();
                return true;
            }
            return false;
        }

        public async Task<string> SaveUploadedFileAsync(IFormFile file)
        {
            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(_uploadsPath, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);
            return filePath;
        }

        public async Task<int> EstimateEmailCountAsync(string filePath)
        {
            try
            {
                using var zip = ZipFile.OpenRead(filePath);
                return zip.Entries.Count(e => e.Name.EndsWith(".eml", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estimating email count for {FilePath}", filePath);
                return 0;
            }
        }

        public void CleanupOldJobs()
        {
            var cutoff = DateTime.UtcNow.AddDays(-7);
            var toRemove = _allJobs.Values.Where(j => j.Completed.HasValue && j.Completed < cutoff).ToList();
            foreach (var job in toRemove)
            {
                _allJobs.TryRemove(job.JobId, out _);
                try { if (File.Exists(job.FilePath)) File.Delete(job.FilePath); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete old EML file"); }
            }
            if (toRemove.Any()) _logger.LogInformation("Cleaned up {Count} old EML import jobs", toRemove.Count);
        }

        // ========================================
        // BackgroundService
        // ========================================

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
                        if (job.Status != EmlImportJobStatus.Cancelled)
                            await ProcessJob(job, stoppingToken);
                    }
                    else await Task.Delay(100, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in EML Import Background Service");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("EML Import Background Service is stopping.");
            return base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            _cleanupTimer?.Dispose();
            _currentJobCancellation?.Dispose();
            base.Dispose();
        }

        // ========================================
        // Job Processing
        // ========================================

        private async Task ProcessJob(EmlImportJob job, CancellationToken stoppingToken)
        {
            _currentJobCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var ct = _currentJobCancellation.Token;

            try
            {
                job.Status = EmlImportJobStatus.Running;
                job.Started = DateTime.UtcNow;
                _logger.LogInformation("Starting EML import job {JobId} for {FileName}", job.JobId, job.FileName);

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                var targetAccount = await context.MailAccounts.FindAsync(job.TargetAccountId);
                if (targetAccount == null)
                    throw new InvalidOperationException("Target account " + job.TargetAccountId + " not found");

                if (job.TotalEmails == 0)
                    job.TotalEmails = await EstimateEmailCountAsync(job.FilePath);

                await ProcessZipFile(job, targetAccount, ct);

                if (job.Status != EmlImportJobStatus.Cancelled)
                {
                    job.Status = EmlImportJobStatus.Completed;
                    job.Completed = DateTime.UtcNow;
                    _logger.LogInformation("Completed job {JobId}. Success: {Success}, Failed: {Failed}, SkippedDuplicates: {Skipped}",
                        job.JobId, job.SuccessCount, job.FailedCount, job.SkippedAlreadyExistsCount);
                }

                TryDeleteFile(job.FilePath);
            }
            catch (OperationCanceledException)
            {
                job.Status = EmlImportJobStatus.Cancelled;
                job.Completed = DateTime.UtcNow;
                DeleteTempFile(job.FilePath, job.JobId);
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

        private async Task ProcessZipFile(EmlImportJob job, MailAccount targetAccount, CancellationToken ct)
        {
            using var zip = ZipFile.OpenRead(job.FilePath);
            var emlEntries = zip.Entries.Where(e => e.Name.EndsWith(".eml", StringComparison.OrdinalIgnoreCase)).ToList();
            job.TotalEmails = emlEntries.Count;

            using var scope = _serviceProvider.CreateScope();
            var mailCleaner = scope.ServiceProvider.GetRequiredService<EmlMailCleaner>();
            var mailImporter = scope.ServiceProvider.GetRequiredService<MailImporter>();

            foreach (var entry in emlEntries)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    string targetFolder = "INBOX";
                    if (!string.IsNullOrEmpty(entry.FullName))
                    {
                        var folderPath = Path.GetDirectoryName(entry.FullName);
                        if (!string.IsNullOrEmpty(folderPath))
                        {
                            var folders = folderPath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                            if (folders.Length > 0) targetFolder = folders[folders.Length - 1];
                        }
                    }

                    using var entryStream = entry.Open();
                    using var memoryStream = new MemoryStream();
                    await entryStream.CopyToAsync(memoryStream, ct);
                    memoryStream.Position = 0;

                    var parser = new MimeParser(memoryStream, MimeFormat.Entity);
                    var message = await parser.ParseMessageAsync(ct);

                    mailCleaner.PreCleanMessage(message);

                    job.CurrentEmailSubject = message.Subject;
                    job.ProcessedBytes = memoryStream.Position;

                    var importResult = await mailImporter.ImportEmailToDatabase(message, targetAccount, job.JobId, targetFolder);
                    message?.Dispose();

                    if (importResult.Success) job.SuccessCount++;
                    else if (importResult.AlreadyExists) job.SkippedAlreadyExistsCount++;
                    else job.FailedCount++;

                    job.ProcessedEmails++;

                    if (job.ProcessedEmails % 50 == 0)
                    {
                        using var ctxScope = _serviceProvider.CreateScope();
                        var ctx = ctxScope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                        ctx.ChangeTracker.Clear();
                    }

                    if (job.ProcessedEmails % 10 == 0 && _batchOptions.PauseBetweenEmailsMs > 0)
                    {
                        await Task.Delay(_batchOptions.PauseBetweenEmailsMs, ct);
                        if (job.ProcessedEmails % 50 == 0) { GC.Collect(); GC.WaitForPendingFinalizers(); }
                    }

                    if (job.ProcessedEmails % 100 == 0)
                    {
                        var pct = job.TotalEmails > 0 ? (job.ProcessedEmails * 100.0 / job.TotalEmails) : 0;
                        _logger.LogInformation("Job {JobId}: {Processed}/{Total} ({Progress:F1}%)",
                            job.JobId, job.ProcessedEmails, job.TotalEmails, pct);
                        using var memScope = _serviceProvider.CreateScope();
                        var ctx = memScope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                        ctx.ChangeTracker.Clear();
                        GC.Collect(); GC.WaitForPendingFinalizers();
                        _logger.LogInformation("Memory: {Mem}", MemoryMonitor.GetMemoryUsageFormatted());
                    }
                }
                catch (FormatException ex)
                {
                    _logger.LogWarning(ex, "Job {JobId}: Skipping malformed email in {Entry}", job.JobId, entry.FullName);
                    job.FailedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Job {JobId}: Error processing {Entry}", job.JobId, entry.FullName);
                    job.FailedCount++;
                }
            }
        }

        private void DeleteTempFile(string filePath, string jobId)
        {
            try { if (File.Exists(filePath)) File.Delete(filePath); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temp EML file"); }
        }

        private void TryDeleteFile(string filePath)
        {
            try { if (File.Exists(filePath)) File.Delete(filePath); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temporary EML file"); }
        }
    }
}

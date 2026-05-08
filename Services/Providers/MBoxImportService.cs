using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Providers.Eml;
using MailArchiver.Services.Providers.MBox;
using MailArchiver.Services.Shared;
using MailArchiver.Utilities;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Collections.Concurrent;
using System.Text;

namespace MailArchiver.Services.Providers
{
    /// <summary>
    /// BackgroundService that processes MBox import jobs from a queue.
    /// Reuses EmlMailCleaner, EmlMailImporter, EmlAttachmentCollector, and EmlTruncatedContentSaver
    /// from the EML provider. MBox-specific stream processing is delegated to MBoxStreamProcessor.
    /// </summary>
    public class MBoxImportService : BackgroundService, IMBoxImportService
    {
private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<MBoxImportService> _logger;
        private readonly BatchOperationOptions _batchOptions;
        private readonly ConcurrentQueue<MBoxImportJob> _jobQueue = new();
        private readonly ConcurrentDictionary<string, MBoxImportJob> _allJobs = new();
        private readonly Timer _cleanupTimer;
        private CancellationTokenSource? _currentJobCancellation;
        private readonly string _uploadsPath;

        public MBoxImportService(IServiceProvider serviceProvider, ILogger<MBoxImportService> logger,
            IWebHostEnvironment environment, IOptions<BatchOperationOptions> batchOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _batchOptions = batchOptions.Value;
            _uploadsPath = Path.Combine(environment.ContentRootPath, "uploads", "mbox");
            Directory.CreateDirectory(_uploadsPath);
            _cleanupTimer = new Timer(_ => CleanupOldJobs(), null, TimeSpan.FromHours(24), TimeSpan.FromHours(24));
        }

        // ========================================
        // IMBoxImportService
        // ========================================

        public string QueueImport(MBoxImportJob job)
        {
            job.Status = MBoxImportJobStatus.Queued;
            _allJobs[job.JobId] = job;
            _jobQueue.Enqueue(job);
            _logger.LogInformation("Queued MBox import job {JobId} for {FileName}", job.JobId, job.FileName);
            return job.JobId;
        }

        public MBoxImportJob? GetJob(string jobId)
            => _allJobs.TryGetValue(jobId, out var job) ? job : null;

        public List<MBoxImportJob> GetActiveJobs()
            => _allJobs.Values.Where(j => j.Status == MBoxImportJobStatus.Queued || j.Status == MBoxImportJobStatus.Running)
                .OrderBy(j => j.Created).ToList();

        public List<MBoxImportJob> GetAllJobs()
            => _allJobs.Values.OrderByDescending(j => j.Status == MBoxImportJobStatus.Running || j.Status == MBoxImportJobStatus.Queued)
                .ThenByDescending(j => j.Created).ToList();

        public bool CancelJob(string jobId)
        {
            if (!_allJobs.TryGetValue(jobId, out var job)) return false;
            if (job.Status == MBoxImportJobStatus.Queued)
            {
                job.Status = MBoxImportJobStatus.Cancelled;
                job.Completed = DateTime.UtcNow;
                DeleteTempFile(job.FilePath, jobId);
                return true;
            }
            if (job.Status == MBoxImportJobStatus.Running)
            {
                job.Status = MBoxImportJobStatus.Cancelled;
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
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192);
                int count = 0;
                string? line;
                while ((line = await reader.ReadLineAsync()) != null && count < 100000)
                {
                    if (line.StartsWith("From ") && line.Contains("@")) count++;
                    if (reader.BaseStream.Position > 10_000_000)
                    {
                        var ratio = (double)reader.BaseStream.Length / reader.BaseStream.Position;
                        count = (int)(count * ratio);
                        break;
                    }
                }
                return count;
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
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete old MBox file"); }
            }
            if (toRemove.Any()) _logger.LogInformation("Cleaned up {Count} old MBox import jobs", toRemove.Count);
        }

        // ========================================
        // BackgroundService
        // ========================================

        public override Task StartAsync(CancellationToken ct)
        {
            _logger.LogInformation("MBox Import Background Service is starting.");
            return base.StartAsync(ct);
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
                        if (job.Status != MBoxImportJobStatus.Cancelled)
                            await ProcessJob(job, stoppingToken);
                    }
                    else await Task.Delay(100, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in MBox Import Background Service");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        public override Task StopAsync(CancellationToken ct)
        {
            _logger.LogInformation("MBox Import Background Service is stopping.");
            return base.StopAsync(ct);
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

        private async Task ProcessJob(MBoxImportJob job, CancellationToken stoppingToken)
        {
            _currentJobCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var ct = _currentJobCancellation.Token;

            try
            {
                job.Status = MBoxImportJobStatus.Running;
                job.Started = DateTime.UtcNow;
                _logger.LogInformation("Starting MBox import job {JobId} for {FileName}", job.JobId, job.FileName);

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                var targetAccount = await context.MailAccounts.FindAsync(job.TargetAccountId);
                if (targetAccount == null)
                    throw new InvalidOperationException("Target account " + job.TargetAccountId + " not found");

                if (job.TotalEmails == 0)
                    job.TotalEmails = await EstimateEmailCountAsync(job.FilePath);

                // Resolve all needed services from a single scope
                var mailCleaner = scope.ServiceProvider.GetRequiredService<EmlMailCleaner>();
                var mailImporter = scope.ServiceProvider.GetRequiredService<MailImporter>();
                var streamProcessor = scope.ServiceProvider.GetRequiredService<MBoxStreamProcessor>();

                await streamProcessor.ProcessMBoxFile(job, targetAccount, ct, async (message, folder) =>
                {
                    mailCleaner.PreCleanMessage(message);
                    var result = await mailImporter.ImportEmailToDatabase(message, targetAccount, job.JobId, folder);

                    // Progress & memory management (shared with EML pattern)
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

                    return result;
                });

                if (job.Status != MBoxImportJobStatus.Cancelled)
                {
                    if (job.FailedCount > 0 || job.SkippedMalformedCount > 0 || job.SkippedAlreadyExistsCount > 0)
                    {
                        job.Status = MBoxImportJobStatus.CompletedWithErrors;
                        job.ErrorMessage = $"{job.SuccessCount} imported, {job.FailedCount} failed, {job.SkippedMalformedCount} malformed, {job.SkippedAlreadyExistsCount} duplicates";
                    }
                    else
                    {
                        job.Status = MBoxImportJobStatus.Completed;
                    }
                    job.Completed = DateTime.UtcNow;
                    _logger.LogInformation("Completed MBox job {JobId}. Success: {Success}, Failed: {Failed}, Malformed: {Malformed}",
                        job.JobId, job.SuccessCount, job.FailedCount, job.SkippedMalformedCount);
                }

                TryDeleteFile(job.FilePath);
            }
            catch (OperationCanceledException)
            {
                job.Status = MBoxImportJobStatus.Cancelled;
                job.Completed = DateTime.UtcNow;
                DeleteTempFile(job.FilePath, job.JobId);
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

        private void DeleteTempFile(string filePath, string jobId)
        {
            try { if (File.Exists(filePath)) File.Delete(filePath); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temp MBox file"); }
        }

        private void TryDeleteFile(string filePath)
        {
            try { if (File.Exists(filePath)) File.Delete(filePath); }
            catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temporary MBox file"); }
        }
    }
}

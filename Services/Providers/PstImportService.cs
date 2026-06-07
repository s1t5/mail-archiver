using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Providers.Eml;
using MailArchiver.Services.Providers.Pst;
using MailArchiver.Services.Shared;

using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace MailArchiver.Services.Providers
{
    public class PstImportService : BackgroundService, IPstImportService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PstImportService> _logger;
        private readonly BatchOperationOptions _batchOptions;
        private readonly ConcurrentQueue<PstImportJob> _jobQueue = new();
        private readonly ConcurrentDictionary<string, PstImportJob> _allJobs = new();
        private readonly Timer _cleanupTimer;
        private CancellationTokenSource? _currentJobCancellation;
        private readonly string _uploadsPath;

        public PstImportService(IServiceProvider serviceProvider, ILogger<PstImportService> logger,
            IWebHostEnvironment environment, IOptions<BatchOperationOptions> batchOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _batchOptions = batchOptions.Value;
            _uploadsPath = Path.Combine(environment.ContentRootPath, "uploads", "pst");
            Directory.CreateDirectory(_uploadsPath);
            _cleanupTimer = new Timer(_ => CleanupOldJobs(), null, TimeSpan.FromHours(24), TimeSpan.FromHours(24));
        }

        // ========================================
        // IPstImportService
        // ========================================

        public string QueueImport(PstImportJob job)
        {
            job.Status = PstImportJobStatus.Queued;
            _allJobs[job.JobId] = job;
            _jobQueue.Enqueue(job);
            _logger.LogInformation("Queued PST import job {JobId} for {FileName}", job.JobId, job.FileName);
            return job.JobId;
        }

        public PstImportJob? GetJob(string jobId)
            => _allJobs.TryGetValue(jobId, out var job) ? job : null;

        public List<PstImportJob> GetActiveJobs()
            => [.. _allJobs.Values
                .Where(j => j.Status is PstImportJobStatus.Queued or PstImportJobStatus.Running)
                .OrderBy(j => j.Created)];

        public List<PstImportJob> GetAllJobs()
            => [.. _allJobs.Values
                .OrderByDescending(j => j.Status is PstImportJobStatus.Running or PstImportJobStatus.Queued)
                .ThenByDescending(j => j.Created)];

        public bool CancelJob(string jobId)
        {
            if (!_allJobs.TryGetValue(jobId, out var job)) return false;
            if (job.Status == PstImportJobStatus.Queued)
            {
                job.Status = PstImportJobStatus.Cancelled;
                job.Completed = DateTime.UtcNow;
                return true;
            }
            if (job.Status == PstImportJobStatus.Running)
            {
                job.Status = PstImportJobStatus.Cancelled;
                _currentJobCancellation?.Cancel();
                return true;
            }
            return false;
        }

        public async Task<string> SaveUploadedFileAsync(IFormFile file)
        {
            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(_uploadsPath, fileName);
            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
            return filePath;
        }

        public void CleanupOldJobs()
        {
            var cutoff = DateTime.UtcNow.AddDays(-7);
            var toRemove = _allJobs.Values
                .Where(j => j.Completed.HasValue && j.Completed < cutoff).ToList();
            foreach (var job in toRemove)
            {
                _allJobs.TryRemove(job.JobId, out _);
            }
            if (toRemove.Count != 0)
                _logger.LogInformation("Cleaned up {Count} old PST import jobs", toRemove.Count);
        }

        // ========================================
        // BackgroundService
        // ========================================

        public override Task StartAsync(CancellationToken ct)
        {
            _logger.LogInformation("PST Import Background Service is starting.");
            return base.StartAsync(ct);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PST Import Background Service started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_jobQueue.TryDequeue(out var job))
                    {
                        if (job.Status != PstImportJobStatus.Cancelled)
                            await ProcessJob(job, stoppingToken);
                    }
                    else await Task.Delay(100, stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PST Import Background Service");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        public override Task StopAsync(CancellationToken ct)
        {
            _logger.LogInformation("PST Import Background Service is stopping.");
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

        private async Task ProcessJob(PstImportJob job, CancellationToken stoppingToken)
        {
            _currentJobCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var ct = _currentJobCancellation.Token;

            try
            {
                job.Status = PstImportJobStatus.Running;
                job.Started = DateTime.UtcNow;
                _logger.LogInformation("Starting PST import job {JobId} for {FileName}", job.JobId, job.FileName);

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                var targetAccount =
                    await context.MailAccounts.FindAsync([job.TargetAccountId], cancellationToken: stoppingToken) ??
                    throw new InvalidOperationException($"Target account {job.TargetAccountId} not found");
                var mailCleaner = scope.ServiceProvider.GetRequiredService<EmlMailCleaner>();
                var mailImporter = scope.ServiceProvider.GetRequiredService<MailImporter>();
                var pstProcessor = scope.ServiceProvider.GetRequiredService<PstProcessor>();

                await pstProcessor.ProcessPstFile(job, targetAccount, ct, async (message, folder) =>
                {
                    mailCleaner.PreCleanMessage(message);
                    var result = await mailImporter.ImportEmailToDatabase(message, targetAccount, job.JobId, folder);

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
                        _logger.LogInformation("PST Job {JobId}: {Processed} emails ({Progress:F1}%)",
                            job.JobId, job.ProcessedEmails, pct);
                        using var memScope = _serviceProvider.CreateScope();
                        var ctx = memScope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
                        ctx.ChangeTracker.Clear();
                        GC.Collect(); GC.WaitForPendingFinalizers();
                        _logger.LogInformation("Memory: {Mem}", MemoryMonitor.GetMemoryUsageFormatted());
                    }

                    return result;
                });

                if (job.Status != PstImportJobStatus.Cancelled)
                {
                    if (job.FailedCount > 0 || job.SkippedAlreadyExistsCount > 0)
                    {
                        job.Status = PstImportJobStatus.CompletedWithErrors;
                        job.ErrorMessage = $"{job.SuccessCount} imported, {job.FailedCount} failed, {job.SkippedAlreadyExistsCount} duplicates";
                    }
                    else
                    {
                        job.Status = PstImportJobStatus.Completed;
                    }
                    job.Completed = DateTime.UtcNow;
                    _logger.LogInformation("Completed PST job {JobId}. Success: {Success}, Failed: {Failed}",
                        job.JobId, job.SuccessCount, job.FailedCount);
                }
            }
            catch (OperationCanceledException)
            {
                job.Status = PstImportJobStatus.Cancelled;
                job.Completed = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                job.Status = PstImportJobStatus.Failed;
                job.Completed = DateTime.UtcNow;
                job.ErrorMessage = ex.Message;
                _logger.LogError(ex, "PST import job {JobId} failed", job.JobId);
            }
            finally
            {
                _currentJobCancellation?.Dispose();
                _currentJobCancellation = null;
            }
        }
    }
}

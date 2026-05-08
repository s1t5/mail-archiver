// Services/BatchRestoreService.cs
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace MailArchiver.Services
{
    public class BatchRestoreService : BackgroundService, IBatchRestoreService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BatchRestoreService> _logger;
        private readonly BatchOperationOptions _batchOptions;
        private readonly ConcurrentQueue<BatchRestoreJob> _jobQueue = new();
        private readonly ConcurrentDictionary<string, BatchRestoreJob> _allJobs = new();
        private readonly Timer _cleanupTimer;
        private CancellationTokenSource? _currentJobCancellation;

        public BatchRestoreService(IServiceProvider serviceProvider, ILogger<BatchRestoreService> logger, IOptions<BatchOperationOptions> batchOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _batchOptions = batchOptions.Value;
            
            // Cleanup-Timer: Jeden Stunde alte Jobs entfernen
            _cleanupTimer = new Timer(
                callback: _ => CleanupOldJobs(),
                state: null,
                dueTime: TimeSpan.FromMinutes(60),
                period: TimeSpan.FromMinutes(60)
            );
        }

        public string QueueJob(BatchRestoreJob job)
        {
            job.Status = BatchRestoreJobStatus.Queued;
            _allJobs[job.JobId] = job;
            _jobQueue.Enqueue(job);
            
            _logger.LogInformation("Queued batch restore job {JobId} with {Count} emails", 
                job.JobId, job.EmailIds.Count);
            
            return job.JobId;
        }

        public BatchRestoreJob? GetJob(string jobId)
        {
            // Handle null jobId to prevent ArgumentNullException
            if (string.IsNullOrEmpty(jobId))
                return null;
                
            return _allJobs.TryGetValue(jobId, out var job) ? job : null;
        }

        public List<BatchRestoreJob> GetActiveJobs()
        {
            return _allJobs.Values
                .Where(j => j.Status == BatchRestoreJobStatus.Queued || j.Status == BatchRestoreJobStatus.Running)
                .OrderBy(j => j.Created)
                .ToList();
        }

        public List<BatchRestoreJob> GetAllJobs()
        {
            return _allJobs.Values
                .OrderByDescending(j => j.Created)
                .ToList();
        }

        public bool CancelJob(string jobId)
        {
            // Handle null jobId to prevent ArgumentNullException
            if (string.IsNullOrEmpty(jobId))
                return false;
                
            if (_allJobs.TryGetValue(jobId, out var job))
            {
                if (job.Status == BatchRestoreJobStatus.Queued)
                {
                    job.Status = BatchRestoreJobStatus.Cancelled;
                    _logger.LogInformation("Cancelled queued job {JobId}", jobId);
                    return true;
                }
                else if (job.Status == BatchRestoreJobStatus.Running)
                {
                    job.Status = BatchRestoreJobStatus.Cancelled;
                    _currentJobCancellation?.Cancel();
                    _logger.LogInformation("Requested cancellation of running job {JobId}", jobId);
                    return true;
                }
            }
            return false;
        }

        public void CleanupOldJobs()
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-24); // Jobs älter als 24 Stunden entfernen
            var toRemove = _allJobs.Values
                .Where(j => j.Completed.HasValue && j.Completed < cutoffTime)
                .Select(j => j.JobId)
                .ToList();

            foreach (var jobId in toRemove)
            {
                _allJobs.TryRemove(jobId, out _);
            }

            if (toRemove.Any())
            {
                _logger.LogInformation("Cleaned up {Count} old batch restore jobs", toRemove.Count);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Batch Restore Background Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_jobQueue.TryDequeue(out var job))
                    {
                        // Prüfe ob Job bereits abgebrochen wurde
                        if (job.Status == BatchRestoreJobStatus.Cancelled)
                        {
                            _logger.LogInformation("Skipping cancelled job {JobId}", job.JobId);
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
                    _logger.LogInformation("Batch Restore Background Service stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Batch Restore Background Service");
                    await Task.Delay(5000, stoppingToken); // Warte 5 Sekunden bei Fehlern
                }
            }
        }

        private async Task ProcessJob(BatchRestoreJob job, CancellationToken stoppingToken)
        {
            _currentJobCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var cancellationToken = _currentJobCancellation.Token;

            try
            {
                job.Status = BatchRestoreJobStatus.Running;
                job.Started = DateTime.UtcNow;
                
                _logger.LogInformation("Starting batch restore job {JobId} with {Count} emails", 
                    job.JobId, job.EmailIds.Count);

                using var scope = _serviceProvider.CreateScope();
                var imapEmailService = scope.ServiceProvider.GetRequiredService<MailArchiver.Services.Providers.ImapEmailService>();
                var providerEmailService = scope.ServiceProvider.GetRequiredService<IProviderEmailService>();
                var dbContext = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

                // Verarbeite in Batches mit Progress-Updates
                await ProcessJobWithProgress(job, imapEmailService, providerEmailService, dbContext, cancellationToken);

                if (job.Status != BatchRestoreJobStatus.Cancelled)
                {
                    job.Status = BatchRestoreJobStatus.Completed;
                    job.Completed = DateTime.UtcNow;
                    
                    _logger.LogInformation("Completed batch restore job {JobId}. Success: {Success}, Failed: {Failed}", 
                        job.JobId, job.SuccessCount, job.FailedCount);
                }
            }
            catch (OperationCanceledException)
            {
                job.Status = BatchRestoreJobStatus.Cancelled;
                job.Completed = DateTime.UtcNow;
                _logger.LogInformation("Batch restore job {JobId} was cancelled", job.JobId);
            }
            catch (Exception ex)
            {
                job.Status = BatchRestoreJobStatus.Failed;
                job.Completed = DateTime.UtcNow;
                job.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Batch restore job {JobId} failed", job.JobId);
            }
            finally
            {
                _currentJobCancellation?.Dispose();
                _currentJobCancellation = null;
            }
        }

        private async Task ProcessJobWithProgress(
            BatchRestoreJob job,
            MailArchiver.Services.Providers.ImapEmailService imapEmailService,
            IProviderEmailService providerEmailService,
            MailArchiverDbContext dbContext,
            CancellationToken cancellationToken)
        {
            var batchSize = _batchOptions.BatchSize;
            var totalEmails = job.EmailIds.Count;

            _logger.LogInformation("Job {JobId}: Starting batch restore with {TotalEmails} emails to account {AccountId}, folder {Folder}, preserveFolderStructure={Preserve}",
                job.JobId, totalEmails, job.TargetAccountId, job.TargetFolder, job.PreserveFolderStructure);

            // Get target account to check provider type - ensure we have a fresh copy from the database
            var targetAccount = await dbContext.MailAccounts
                .Where(a => a.Id == job.TargetAccountId)
                .FirstOrDefaultAsync(cancellationToken);

            if (targetAccount == null)
            {
                _logger.LogError("Job {JobId}: Target account with ID {AccountId} not found", job.JobId, job.TargetAccountId);
                throw new InvalidOperationException($"Target account with ID {job.TargetAccountId} not found");
            }

            _logger.LogInformation("Job {JobId}: Target account found - Name: {AccountName}, Provider: {Provider}, Enabled: {Enabled}",
                job.JobId, targetAccount.Name, targetAccount.Provider, targetAccount.IsEnabled);

            if (!targetAccount.IsEnabled)
            {
                _logger.LogError("Job {JobId}: Target account {AccountId} is disabled", job.JobId, job.TargetAccountId);
                throw new InvalidOperationException($"Target account '{targetAccount.Name}' is disabled");
            }

            var isM365Account = targetAccount.Provider == ProviderType.M365;
            _logger.LogInformation("Job {JobId}: Using {ServiceType} for {ProviderType} account",
                job.JobId, isM365Account ? "Graph API" : "IMAP", targetAccount.Provider);

            // Handle IMAP accounts with optimized shared connection approach
            if (!isM365Account)
            {
                _logger.LogInformation("Job {JobId}: Using optimized IMAP batch restore with shared connection for {Count} emails",
                    job.JobId, job.EmailIds.Count);

                try
                {
                    // Create progress callback for IMAP restore
                    Action<int, int, int> progressCallback = (processed, successful, failed) =>
                    {
                        job.ProcessedCount = processed;
                        job.SuccessCount = successful;
                        job.FailedCount = failed;
                        
                        // Log progress every 10 emails or at the end
                        if (processed % 10 == 0 || processed == totalEmails)
                        {
                            _logger.LogInformation("Job {JobId}: IMAP Progress - {Processed}/{Total} emails processed. Success: {Success}, Failed: {Failed}",
                                job.JobId, processed, totalEmails, successful, failed);
                        }
                    };

                    var (successful, failed) = await imapEmailService.RestoreMultipleEmailsWithProgressAsync(
                        job.EmailIds, job.TargetAccountId, job.TargetFolder, job.PreserveFolderStructure, progressCallback, cancellationToken);

                    job.SuccessCount = successful;
                    job.FailedCount = failed;
                    job.ProcessedCount = successful + failed;

                    _logger.LogInformation("Job {JobId}: IMAP batch restore completed. Success: {Success}, Failed: {Failed}",
                        job.JobId, job.SuccessCount, job.FailedCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Job {JobId}: Critical error during IMAP batch restore: {Message}", job.JobId, ex.Message);
                    job.FailedCount = job.EmailIds.Count;
                    job.ProcessedCount = job.EmailIds.Count;
                }
            }
            else
            {
                // Handle M365 accounts with optimized batch processing using Graph API
                // Each batch pre-fetches the folder hierarchy once, avoiding redundant API calls
                _logger.LogInformation("Job {JobId}: Using optimized Graph API batch restore for M365 account", job.JobId);

                for (int i = 0; i < totalEmails; i += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var batch = job.EmailIds.Skip(i).Take(batchSize).ToList();
                    
                    var batchNumber = (i / batchSize) + 1;
                    var totalBatches = (totalEmails + batchSize - 1) / batchSize;

                    _logger.LogInformation("Job {JobId}: Processing batch {Current}/{Total} ({BatchStart}-{BatchEnd} of {TotalEmails})",
                        job.JobId, batchNumber, totalBatches, i + 1, Math.Min(i + batchSize, totalEmails), totalEmails);

                    _logger.LogInformation("Job {JobId}: Using optimized Graph API batch restore for {Count} emails (folders pre-fetched once per batch)",
                        job.JobId, batch.Count);

                    try
                    {
                        var successCountBeforeBatch = job.SuccessCount;
                        var failedCountBeforeBatch = job.FailedCount;
                        var startingProcessedCount = job.ProcessedCount;

                        Action<int, int, int> batchProgressCallback = (processed, successful, failed) =>
                        {
                            job.SuccessCount = successCountBeforeBatch + successful;
                            job.FailedCount = failedCountBeforeBatch + failed;
                            job.ProcessedCount = startingProcessedCount + processed;
                            
                            if (processed % 10 == 0 || processed == batch.Count)
                            {
                                _logger.LogInformation("Job {JobId}: Progress - {Processed}/{Total} emails processed. Success: {Success}, Failed: {Failed}",
                                    job.JobId, job.ProcessedCount, totalEmails, job.SuccessCount, job.FailedCount);
                            }
                        };

                        var (batchSuccessful, batchFailed) = await providerEmailService.RestoreMultipleEmailsWithProgressAsync(
                            batch, job.TargetAccountId, job.TargetFolder, job.PreserveFolderStructure, batchProgressCallback, cancellationToken);

                        job.SuccessCount = successCountBeforeBatch + batchSuccessful;
                        job.FailedCount = failedCountBeforeBatch + batchFailed;
                        job.ProcessedCount = startingProcessedCount + batchSuccessful + batchFailed;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Job {JobId}: Critical error during M365 batch restore: {Message}", job.JobId, ex.Message);
                        // Mark all emails in this batch as failed
                        job.FailedCount += batch.Count;
                        job.ProcessedCount += batch.Count;
                    }

                    // Pause between emails within a batch for M365
                    if (_batchOptions.PauseBetweenEmailsMs > 0)
                    {
                        _logger.LogDebug("Job {JobId}: Pausing {Ms}ms between emails", job.JobId, _batchOptions.PauseBetweenEmailsMs);
                        await Task.Delay(_batchOptions.PauseBetweenEmailsMs, cancellationToken);
                    }

                    // Pause between batches
                    if (i + batchSize < totalEmails && _batchOptions.PauseBetweenBatchesMs > 0)
                    {
                        _logger.LogDebug("Job {JobId}: Pausing {Ms}ms between batches", job.JobId, _batchOptions.PauseBetweenBatchesMs);
                        await Task.Delay(_batchOptions.PauseBetweenBatchesMs, cancellationToken);
                    }
                }
            }

            _logger.LogInformation("Job {JobId}: Batch restore completed. Total: {Total}, Success: {Success}, Failed: {Failed}",
                job.JobId, totalEmails, job.SuccessCount, job.FailedCount);
        }

        public override void Dispose()
        {
            _cleanupTimer?.Dispose();
            _currentJobCancellation?.Dispose();
            base.Dispose();
        }
    }
}
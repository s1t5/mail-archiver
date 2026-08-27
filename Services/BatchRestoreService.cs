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

        // One cancellation source per job. A single shared field meant that cancelling a
        // specific job id actually cancelled whichever job happened to be running at that
        // moment, which is wrong even with a single worker and plainly broken with several.
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobCancellations = new();

        // At most one job per target account may run at a time. Two jobs appending into the
        // same mailbox would each take their own duplicate index snapshot before the other
        // started writing, and could then both append the same mail. The idempotency guarantee
        // depends on this lock.
        private readonly ConcurrentDictionary<int, byte> _busyTargetAccounts = new();

        private readonly OffloadOptions _offloadOptions;

        public BatchRestoreService(IServiceProvider serviceProvider, ILogger<BatchRestoreService> logger, IOptions<BatchOperationOptions> batchOptions, IOptions<OffloadOptions> offloadOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _batchOptions = batchOptions.Value;
            _offloadOptions = offloadOptions.Value;
            
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
                    if (_jobCancellations.TryGetValue(jobId, out var cts))
                    {
                        cts.Cancel();
                    }
                    else
                    {
                        _logger.LogWarning("Job {JobId} is marked running but has no cancellation source", jobId);
                    }
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

            // MaxConcurrentJobs defaults to 1, which reproduces the strictly serial behaviour
            // this loop always had. Raising it lets independent target mailboxes be filled in
            // parallel; the per-target lock below keeps two jobs off the same mailbox.
            var slots = new SemaphoreSlim(Math.Max(1, _offloadOptions.MaxConcurrentJobs));
            var running = new List<Task>();

            _logger.LogInformation("Batch restore concurrency: {Max} concurrent job(s)",
                Math.Max(1, _offloadOptions.MaxConcurrentJobs));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    running.RemoveAll(t => t.IsCompleted);

                    if (_jobQueue.TryDequeue(out var job))
                    {
                        // Prüfe ob Job bereits abgebrochen wurde
                        if (job.Status == BatchRestoreJobStatus.Cancelled)
                        {
                            _logger.LogInformation("Skipping cancelled job {JobId}", job.JobId);
                            continue;
                        }

                        // A job whose target mailbox is already being written to goes back on
                        // the queue rather than running: see _busyTargetAccounts.
                        if (!_busyTargetAccounts.TryAdd(job.TargetAccountId, 0))
                        {
                            _logger.LogDebug(
                                "Target account {AccountId} is busy, requeueing job {JobId}",
                                job.TargetAccountId, job.JobId);
                            _jobQueue.Enqueue(job);
                            await Task.Delay(1000, stoppingToken);
                            continue;
                        }

                        await slots.WaitAsync(stoppingToken);

                        running.Add(Task.Run(async () =>
                        {
                            try
                            {
                                await ProcessJob(job, stoppingToken);
                            }
                            finally
                            {
                                _busyTargetAccounts.TryRemove(job.TargetAccountId, out _);
                                slots.Release();
                            }
                        }, stoppingToken));
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

            // Let jobs already in flight finish rather than tearing their IMAP connections down
            // mid-append.
            try
            {
                await Task.WhenAll(running.Where(t => !t.IsCompleted));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while draining in-flight batch restore jobs on shutdown");
            }
        }

        private async Task ProcessJob(BatchRestoreJob job, CancellationToken stoppingToken)
        {
            var jobCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _jobCancellations[job.JobId] = jobCancellation;
            var cancellationToken = jobCancellation.Token;

            try
            {
                job.Status = BatchRestoreJobStatus.Running;
                job.Started = DateTime.UtcNow;

                using var scope = _serviceProvider.CreateScope();

                // An offload job carries criteria rather than ids and resolves its own set here.
                // Full scans over a few hundred thousand rows take milliseconds against the
                // SentDate index, so there is no reason to push id lists through session state.
                if (job.IsOffload)
                {
                    await ResolveOffloadEmailIdsAsync(job, scope.ServiceProvider, cancellationToken);
                }

                _logger.LogInformation("Starting batch restore job {JobId} with {Count} emails",
                    job.JobId, job.EmailIds.Count);

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
                _jobCancellations.TryRemove(job.JobId, out _);
                jobCancellation.Dispose();
            }
        }

        /// <summary>
        /// Turns an offload job's criteria into the concrete set of archived mail it applies to,
        /// and writes one audit entry recording what was resolved.
        /// </summary>
        private async Task ResolveOffloadEmailIdsAsync(
            BatchRestoreJob job, IServiceProvider services, CancellationToken cancellationToken)
        {
            var criteria = job.Offload!;
            var dbContext = services.GetRequiredService<MailArchiverDbContext>();

            var query = dbContext.ArchivedEmails
                .Where(e => e.MailAccountId == criteria.SourceAccountId)
                .Where(e => e.SentDate >= criteria.CutoffFrom);

            if (criteria.CutoffTo.HasValue)
            {
                // Same inclusive-to-end-of-day semantics the search filter uses.
                var upper = criteria.CutoffTo.Value.Date.AddDays(1).AddSeconds(-1);
                query = query.Where(e => e.SentDate <= upper);
            }

            job.EmailIds = await query.OrderBy(e => e.Id)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken);

            _logger.LogInformation(
                "Job {JobId}: offload criteria resolved to {Count} mails from account {SourceId}, window {Window}",
                job.JobId, job.EmailIds.Count, criteria.SourceAccountId, criteria.DescribeWindow());

            try
            {
                var accessLog = services.GetRequiredService<IAccessLogService>();
                await accessLog.LogAccessAsync(
                    job.UserId,
                    AccessLogType.Restore,
                    mailAccountId: criteria.SourceAccountId,
                    searchParameters: $"offload source={criteria.SourceAccountId} target={job.TargetAccountId} " +
                                $"window={criteria.DescribeWindow()} folder={job.TargetFolder} " +
                                $"preserve={job.PreserveFolderStructure} dryRun={criteria.DryRun} " +
                                $"markAsSeen={criteria.MarkAsSeen} resolved={job.EmailIds.Count}");
            }
            catch (Exception ex)
            {
                // An audit entry must never be the reason a migration job fails.
                _logger.LogWarning(ex, "Job {JobId}: could not write the offload audit entry", job.JobId);
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

                    if (job.IsOffload)
                    {
                        var outcome = await imapEmailService.OffloadEmailsAsync(
                            job.EmailIds, job.TargetAccountId, job.TargetFolder,
                            job.PreserveFolderStructure, job.Offload!, progressCallback, cancellationToken);

                        job.AppendedCount = outcome.Appended;
                        job.SkippedAlreadyPresentCount = outcome.SkippedAlreadyPresent;
                        job.MatchedByFingerprintCount = outcome.MatchedByFingerprint;
                        job.SkippedExcludedFolderCount = outcome.SkippedExcludedFolder;
                        job.SuccessCount = outcome.Appended;
                        job.FailedCount = outcome.Failed;
                        job.ProcessedCount = outcome.Considered;
                        job.Report = outcome.Describe();

                        _logger.LogInformation("Job {JobId}: offload completed. {Report}",
                            job.JobId, job.Report.Replace(Environment.NewLine, " | "));
                        return;
                    }

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
            foreach (var cts in _jobCancellations.Values)
            {
                try { cts.Dispose(); } catch { /* already disposed */ }
            }
            _jobCancellations.Clear();
            base.Dispose();
        }
    }
}
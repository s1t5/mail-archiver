using MailArchiver.Models;
using MailArchiver.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace MailArchiver.Services
{
    public class SyncJobService : ISyncJobService
    {
        private readonly ConcurrentDictionary<string, SyncJob> _jobs = new();
        private readonly ConcurrentDictionary<int, string> _activeAccountJobs = new(); // Track active jobs per account
        private readonly ILogger<SyncJobService> _logger;
        private readonly Timer _cleanupTimer;
        private readonly IServiceProvider _serviceProvider;
        private readonly SyncJobOptions _options;
        private readonly SemaphoreSlim _cleanupSemaphore = new(1, 1);
        private readonly object _jobCountLock = new object();
        private int _runningJobCount = 0;
        private int _completedJobCount = 0;
        private int _failedJobCount = 0;

        public SyncJobService(
            ILogger<SyncJobService> logger, 
            IServiceProvider serviceProvider,
            IOptions<SyncJobOptions> options = null)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _options = options?.Value ?? new SyncJobOptions();
            
            // Cleanup-Timer: Configurable interval for removing old jobs
            _cleanupTimer = new Timer(
                callback: _ => CleanupOldJobsAsync().ConfigureAwait(false),
                state: null,
                dueTime: _options.CleanupInitialDelay,
                period: _options.CleanupInterval
            );
            
            _logger.LogInformation("SyncJobService initialized with cleanup interval: {CleanupInterval}", _options.CleanupInterval);
        }

        public async Task<string?> StartSyncAsync(int accountId, string accountName, DateTime? lastSync = null, CancellationToken cancellationToken = default)
        {
            // Validate that the account exists in the database
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
            
            try
            {
                var accountExists = await dbContext.MailAccounts
                    .AnyAsync(a => a.Id == accountId && a.IsEnabled && a.Provider != ProviderType.IMPORT, cancellationToken);
                
                if (!accountExists)
                {
                    _logger.LogWarning("Cannot start sync job for account {AccountId} ({AccountName}) - account does not exist or is not enabled", accountId, accountName);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating account {AccountId} ({AccountName})", accountId, accountName);
                return null;
            }

            // Check if there's already an active job for this account
            if (_activeAccountJobs.ContainsKey(accountId))
            {
                var existingJobId = _activeAccountJobs[accountId];
                if (_jobs.TryGetValue(existingJobId, out var existingJob) && 
                    existingJob.Status == SyncJobStatus.Running)
                {
                    _logger.LogWarning("Sync job for account {AccountId} ({AccountName}) is already running", accountId, accountName);
                    throw new InvalidOperationException($"A sync job for account {accountName} is already running.");
                }
            }

            // Check if we've reached the maximum concurrent jobs limit
            if (_options.MaxConcurrentJobs > 0)
            {
                lock (_jobCountLock)
                {
                    if (_runningJobCount >= _options.MaxConcurrentJobs)
                    {
                        _logger.LogWarning("Cannot start sync job for account {AccountId} ({AccountName}) - maximum concurrent jobs limit ({MaxJobs}) reached", 
                            accountId, accountName, _options.MaxConcurrentJobs);
                        throw new InvalidOperationException($"Maximum concurrent jobs limit ({_options.MaxConcurrentJobs}) reached. Please try again later.");
                    }
                }
            }

            var job = new SyncJob
            {
                MailAccountId = accountId,
                AccountName = accountName,
                LastSync = lastSync,
                CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken),
                Priority = _options.DefaultJobPriority
            };

            _jobs[job.JobId] = job;
            _activeAccountJobs[accountId] = job.JobId;
            
            lock (_jobCountLock)
            {
                _runningJobCount++;
            }
            
            _logger.LogInformation("Started sync job {JobId} for account {AccountName} with priority {Priority}", 
                job.JobId, accountName, job.Priority);
            
            // Log current job statistics
            LogJobStatistics();
            
            return job.JobId;
        }

        public string StartSync(int accountId, string accountName, DateTime? lastSync = null)
        {
            // Legacy method - delegates to async version
            var result = StartSyncAsync(accountId, accountName, lastSync).GetAwaiter().GetResult();
            if (result == null)
            {
                throw new InvalidOperationException($"Cannot start sync job for account {accountName} - account does not exist or is not enabled");
            }
            return result;
        }

        public SyncJob? GetJob(string jobId)
        {
            return _jobs.TryGetValue(jobId, out var job) ? job : null;
        }

        public List<SyncJob> GetActiveJobs()
        {
            return _jobs.Values
                .Where(j => j.Status == SyncJobStatus.Running)
                .OrderByDescending(j => j.Priority)
                .ThenBy(j => j.Started)
                .ToList();
        }

        public List<SyncJob> GetAllJobs()
        {
            return _jobs.Values
                .OrderByDescending(j => j.Started)
                .ToList();
        }

        public void UpdateJobProgress(string jobId, Action<SyncJob> updateAction)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                var previousProgress = job.ProgressPercentage;
                updateAction(job);
                
                // Log significant progress changes
                if (job.ProgressPercentage - previousProgress >= _options.ProgressLogThreshold)
                {
                    _logger.LogInformation("Job {JobId} progress: {Progress}%", jobId, job.ProgressPercentage);
                }
            }
            else
            {
                _logger.LogWarning("Attempted to update progress for non-existent job {JobId}", jobId);
            }
        }

        public void CompleteJob(string jobId, bool success, string? errorMessage = null)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                var previousStatus = job.Status;
                job.Status = success ? SyncJobStatus.Completed : SyncJobStatus.Failed;
                job.Completed = DateTime.UtcNow;
                job.ErrorMessage = errorMessage;
                
                // Remove from active account jobs
                _activeAccountJobs.TryRemove(job.MailAccountId, out _);
                
                // Update job statistics
                lock (_jobCountLock)
                {
                    if (previousStatus == SyncJobStatus.Running)
                    {
                        _runningJobCount--;
                    }
                    
                    if (success)
                    {
                        _completedJobCount++;
                    }
                    else
                    {
                        _failedJobCount++;
                    }
                }
                
                // Log job completion with duration
                var duration = job.Completed - job.Started;
                _logger.LogInformation("Completed sync job {JobId} with status {Status} in {Duration}ms", 
                    jobId, job.Status, duration?.TotalMilliseconds);
                
                // Log error details if job failed
                if (!success && !string.IsNullOrEmpty(errorMessage))
                {
                    _logger.LogError("Job {JobId} failed with error: {Error}", jobId, errorMessage);
                }
                
                // Schedule retry if configured and job failed
                if (!success && _options.EnableAutoRetry && job.RetryCount < _options.MaxRetryAttempts)
                {
                    ScheduleRetry(job);
                }
                
                // Log current job statistics
                LogJobStatistics();
            }
            else
            {
                _logger.LogWarning("Attempted to complete non-existent job {JobId}", jobId);
            }
        }

        public bool CancelJob(string jobId)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                if (job.Status == SyncJobStatus.Running)
                {
                    // Set status to cancelled first
                    job.Status = SyncJobStatus.Cancelled;
                    
                    // Cancel the token source if it exists
                    if (job.CancellationTokenSource != null)
                    {
                        try
                        {
                            job.CancellationTokenSource.Cancel();
                        }
                        catch (ObjectDisposedException)
                        {
                            // Token source might already be disposed, that's okay
                            _logger.LogDebug("Token source for job {JobId} was already disposed", jobId);
                        }
                    }
                    
                    // Remove from active account jobs
                    _activeAccountJobs.TryRemove(job.MailAccountId, out _);
                    
                    // Update job statistics
                    lock (_jobCountLock)
                    {
                        _runningJobCount--;
                    }
                    
                    _logger.LogInformation("Cancelled sync job {JobId} for account {AccountName}", jobId, job.AccountName);
                    LogJobStatistics();
                    return true;
                }
                else
                {
                    _logger.LogWarning("Cannot cancel job {JobId} because it's not running. Current status: {Status}", jobId, job.Status);
                }
            }
            else
            {
                _logger.LogWarning("Cannot cancel job {JobId} because it doesn't exist", jobId);
            }
            return false;
        }

        public bool CancelJobsForAccount(int accountId)
        {
            bool anyCancelled = false;
            var jobsToCancel = _jobs.Values
                .Where(j => j.MailAccountId == accountId && j.Status == SyncJobStatus.Running)
                .ToList();

            foreach (var job in jobsToCancel)
            {
                if (CancelJob(job.JobId))
                {
                    anyCancelled = true;
                }
            }

            if (anyCancelled)
            {
                _logger.LogInformation("Cancelled {Count} running sync jobs for account {AccountId}", jobsToCancel.Count, accountId);
            }

            return anyCancelled;
        }

        public async Task CleanupOldJobsAsync()
        {
            // Ensure only one cleanup operation runs at a time
            if (!await _cleanupSemaphore.WaitAsync(100))
            {
                _logger.LogDebug("Cleanup operation already in progress, skipping");
                return;
            }

            try
            {
                var cutoffTime = DateTime.UtcNow.Subtract(_options.JobRetentionPeriod);
                var toRemove = _jobs.Values
                    .Where(j => j.Completed.HasValue && j.Completed < cutoffTime)
                    .Select(j => j.JobId)
                    .ToList();

                foreach (var jobId in toRemove)
                {
                    if (_jobs.TryRemove(jobId, out var job))
                    {
                        // Only remove from active account jobs if it's still there
                        _activeAccountJobs.TryRemove(job.MailAccountId, out _);
                        
                        // Dispose the cancellation token source
                        try
                        {
                            job.CancellationTokenSource?.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Error disposing cancellation token for job {JobId}", jobId);
                        }
                    }
                }

                if (toRemove.Any())
                {
                    _logger.LogInformation("Cleaned up {Count} old sync jobs older than {RetentionPeriod}", 
                        toRemove.Count, _options.JobRetentionPeriod);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during job cleanup");
            }
            finally
            {
                _cleanupSemaphore.Release();
            }
        }

        public void CleanupOldJobs()
        {
            // Synchronous version for backward compatibility
            CleanupOldJobsAsync().GetAwaiter().GetResult();
        }

        public SyncJobStatistics GetJobStatistics()
        {
            lock (_jobCountLock)
            {
                return new SyncJobStatistics
                {
                    RunningJobs = _runningJobCount,
                    CompletedJobs = _completedJobCount,
                    FailedJobs = _failedJobCount,
                    TotalJobs = _jobs.Count
                };
            }
        }

        private void ScheduleRetry(SyncJob failedJob)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(_options.RetryDelaySeconds, failedJob.RetryCount + 1));
            
            _logger.LogInformation("Scheduling retry {RetryCount} for job {JobId} in {Delay}s", 
                failedJob.RetryCount + 1, failedJob.JobId, delay.TotalSeconds);
            
            Task.Delay(delay).ContinueWith(async _ =>
            {
                try
                {
                    // Create a new job based on the failed one
                    var retryJob = await StartSyncAsync(
                        failedJob.MailAccountId, 
                        failedJob.AccountName, 
                        failedJob.LastSync);
                    
                    if (retryJob != null)
                    {
                        // Update the retry count
                        UpdateJobProgress(retryJob, job => job.RetryCount = failedJob.RetryCount + 1);
                        _logger.LogInformation("Started retry {RetryCount} for job {JobId}", 
                            failedJob.RetryCount + 1, retryJob);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to schedule retry for job {JobId}", failedJob.JobId);
                }
            }, TaskScheduler.Default);
        }

        private void LogJobStatistics()
        {
            if (_options.EnableStatisticsLogging)
            {
                var stats = GetJobStatistics();
                _logger.LogInformation("Job Statistics - Running: {Running}, Completed: {Completed}, Failed: {Failed}, Total: {Total}",
                    stats.RunningJobs, stats.CompletedJobs, stats.FailedJobs, stats.TotalJobs);
            }
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _cleanupSemaphore?.Dispose();
            
            // Dispose all cancellation token sources
            foreach (var job in _jobs.Values)
            {
                try
                {
                    job.CancellationTokenSource?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error disposing cancellation token for job {JobId}", job.JobId);
                }
            }
        }
    }

    // Configuration options for SyncJobService
    public class SyncJobOptions
    {
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan CleanupInitialDelay { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan JobRetentionPeriod { get; set; } = TimeSpan.FromDays(1);
        public int MaxConcurrentJobs { get; set; } = 0; // 0 means unlimited
        public int DefaultJobPriority { get; set; } = 0;
        public bool EnableAutoRetry { get; set; } = false;
        public int MaxRetryAttempts { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 30;
        public int ProgressLogThreshold { get; set; } = 10; // Log progress every 10%
        public bool EnableStatisticsLogging { get; set; } = true;
    }

    // Statistics for monitoring
    public class SyncJobStatistics
    {
        public int RunningJobs { get; set; }
        public int CompletedJobs { get; set; }
        public int FailedJobs { get; set; }
        public int TotalJobs { get; set; }
    }
}

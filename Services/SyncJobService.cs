using MailArchiver.Models;
using MailArchiver.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace MailArchiver.Services
{
    public class SyncJobService : ISyncJobService
    {
        private readonly ConcurrentDictionary<string, SyncJob> _jobs = new();
        private readonly ConcurrentDictionary<int, string> _activeAccountJobs = new(); // Track active jobs per account
        private readonly ILogger<SyncJobService> _logger;
        private readonly Timer _cleanupTimer;
        private readonly IServiceProvider _serviceProvider;

        public SyncJobService(ILogger<SyncJobService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            
            // Cleanup-Timer: Jeden Stunde alte Jobs entfernen
            _cleanupTimer = new Timer(
                callback: _ => CleanupOldJobs(),
                state: null,
                dueTime: TimeSpan.FromHours(1),
                period: TimeSpan.FromHours(1)
            );
        }

        public async Task<string?> StartSyncAsync(int accountId, string accountName, DateTime? lastSync = null)
        {
            // Validate that the account exists in the database
            // Note: We don't check IsEnabled here to allow manual sync for disabled accounts
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
            
            var accountExists = await dbContext.MailAccounts
                .AnyAsync(a => a.Id == accountId && a.Provider != ProviderType.IMPORT);
            
            if (!accountExists)
            {
                _logger.LogWarning("Cannot start sync job for account {AccountId} ({AccountName}) - account does not exist or is an import-only account", accountId, accountName);
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

            var job = new SyncJob
            {
                MailAccountId = accountId,
                AccountName = accountName,
                LastSync = lastSync
            };

            _jobs[job.JobId] = job;
            _activeAccountJobs[accountId] = job.JobId;
            _logger.LogInformation("Started sync job {JobId} for account {AccountName}", job.JobId, accountName);
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
                .OrderBy(j => j.Started)
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
                updateAction(job);
            }
        }

        public void CompleteJob(string jobId, bool success, string? errorMessage = null)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = success ? SyncJobStatus.Completed : SyncJobStatus.Failed;
                job.Completed = DateTime.UtcNow;
                job.ErrorMessage = errorMessage;
                
                // Remove from active account jobs
                _activeAccountJobs.TryRemove(job.MailAccountId, out _);
                
                _logger.LogInformation("Completed sync job {JobId} with status {Status}", 
                    jobId, job.Status);
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
                    _logger.LogInformation("Cancelled sync job {JobId} for account {AccountName}", jobId, job.AccountName);
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

        public void CleanupOldJobs()
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-24);
            var toRemove = _jobs.Values
                .Where(j => j.Completed.HasValue && j.Completed < cutoffTime)
                .Select(j => j.JobId)
                .ToList();

            foreach (var jobId in toRemove)
            {
                if (_jobs.TryGetValue(jobId, out var job))
                {
                    _activeAccountJobs.TryRemove(job.MailAccountId, out _);
                }
                _jobs.TryRemove(jobId, out _);
            }

            if (toRemove.Any())
            {
                _logger.LogInformation("Cleaned up {Count} old sync jobs", toRemove.Count);
            }
        }
    }
}

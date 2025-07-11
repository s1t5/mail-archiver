using MailArchiver.Models;
using System.Collections.Concurrent;

namespace MailArchiver.Services
{
    public class SyncJobService : ISyncJobService
    {
        private readonly ConcurrentDictionary<string, SyncJob> _jobs = new();
        private readonly ILogger<SyncJobService> _logger;
        private readonly Timer _cleanupTimer;

        public SyncJobService(ILogger<SyncJobService> logger)
        {
            _logger = logger;
            
            // Cleanup-Timer: Jeden Stunde alte Jobs entfernen
            _cleanupTimer = new Timer(
                callback: _ => CleanupOldJobs(),
                state: null,
                dueTime: TimeSpan.FromHours(1),
                period: TimeSpan.FromHours(1)
            );
        }

        public string StartSync(int accountId, string accountName, DateTime? lastSync = null)
        {
            var job = new SyncJob
            {
                MailAccountId = accountId,
                AccountName = accountName,
                LastSync = lastSync
            };

            _jobs[job.JobId] = job;
            _logger.LogInformation("Started sync job {JobId} for account {AccountName}", job.JobId, accountName);
            return job.JobId;
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
                
                _logger.LogInformation("Completed sync job {JobId} with status {Status}", 
                    jobId, job.Status);
            }
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
                _jobs.TryRemove(jobId, out _);
            }

            if (toRemove.Any())
            {
                _logger.LogInformation("Cleaned up {Count} old sync jobs", toRemove.Count);
            }
        }
    }
}
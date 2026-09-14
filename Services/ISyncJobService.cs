using MailArchiver.Models;

namespace MailArchiver.Services
{
    public interface ISyncJobService
    {
        Task<string?> StartSyncAsync(int accountId, string accountName, DateTime? lastSync = null, string? userId = null);
        string StartSync(int accountId, string accountName, DateTime? lastSync = null, string? userId = null);
        SyncJob? GetJob(string jobId);
        List<SyncJob> GetActiveJobs();
        List<SyncJob> GetAllJobs();
        bool IsAccountSyncing(int accountId);
        void UpdateJobProgress(string jobId, Action<SyncJob> updateAction);
        void CompleteJob(string jobId, bool success, string? errorMessage = null);
        void CompleteJobRateLimited(string jobId, string? errorMessage = null);

        /// <summary>
        /// Ends a job that ran past its configured sync timeout. Like the rate-limited path this is
        /// a pause, not a failure: checkpoints are left in place and LastSync is not advanced, so the
        /// next scheduled run resumes where this one stopped.
        /// </summary>
        void CompleteJobTimedOut(string jobId, string? errorMessage = null);
        bool CancelJob(string jobId);
        bool CancelJobsForAccount(int accountId);
        bool AcknowledgeJobFailures(string jobId);
        /// <summary>
        /// The last run of this account that reached an end, whatever kind of end. Kept beyond the
        /// 24-hour job retention, because the account page is the one place where "what did the last
        /// run do" is asked and an empty answer there is worse than an old one. Null only when the
        /// account has not finished a run since the process started.
        /// </summary>
        SyncJob? GetLastCompletedJobForAccount(int accountId);

        void CleanupOldJobs();
    }
}

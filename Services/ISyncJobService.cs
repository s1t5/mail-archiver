using MailArchiver.Models;

namespace MailArchiver.Services
{
    public interface ISyncJobService
    {
        Task<string?> StartSyncAsync(int accountId, string accountName, DateTime? lastSync = null);
        string StartSync(int accountId, string accountName, DateTime? lastSync = null);
        SyncJob? GetJob(string jobId);
        List<SyncJob> GetActiveJobs();
        List<SyncJob> GetAllJobs();
        void UpdateJobProgress(string jobId, Action<SyncJob> updateAction);
        void CompleteJob(string jobId, bool success, string? errorMessage = null);
        bool CancelJob(string jobId);
        bool CancelJobsForAccount(int accountId);
        void CleanupOldJobs();
    }
}

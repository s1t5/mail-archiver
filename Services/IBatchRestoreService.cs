// Services/IBatchRestoreService.cs
using MailArchiver.Models;

namespace MailArchiver.Services
{
    public interface IBatchRestoreService
    {
        string QueueJob(BatchRestoreJob job);
        BatchRestoreJob? GetJob(string jobId);
        List<BatchRestoreJob> GetActiveJobs();
        List<BatchRestoreJob> GetAllJobs();
        bool CancelJob(string jobId);
        void CleanupOldJobs();
    }
}

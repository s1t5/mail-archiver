using MailArchiver.Models;

namespace MailArchiver.Services
{
    public interface IMailAccountDeletionService
    {
        /// <summary>
        /// Queue a mail account for deletion
        /// </summary>
        string QueueDeletion(int mailAccountId, string mailAccountName, string userId);

        /// <summary>
        /// Get the status of a deletion job
        /// </summary>
        MailAccountDeletionJob? GetJob(string jobId);

        /// <summary>
        /// Cancel a deletion job if it hasn't started yet
        /// </summary>
        bool CancelJob(string jobId);

        /// <summary>
        /// Get all jobs for monitoring (admin only)
        /// </summary>
        IEnumerable<MailAccountDeletionJob> GetAllJobs();
    }
}

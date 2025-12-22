using MailArchiver.Models;
using System.Collections.Generic;

namespace MailArchiver.Services
{
    public interface IEmailDeletionService
    {
        /// <summary>
        /// Queues a new email deletion job
        /// </summary>
        string QueueJob(EmailDeletionJob job);

        /// <summary>
        /// Gets a specific deletion job by ID
        /// </summary>
        EmailDeletionJob? GetJob(string jobId);

        /// <summary>
        /// Gets all deletion jobs
        /// </summary>
        List<EmailDeletionJob> GetAllJobs();

        /// <summary>
        /// Gets all active (queued or running) deletion jobs
        /// </summary>
        List<EmailDeletionJob> GetActiveJobs();

        /// <summary>
        /// Cancels a deletion job
        /// </summary>
        bool CancelJob(string jobId);
    }
}

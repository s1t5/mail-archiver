using MailArchiver.Models;
using Microsoft.Graph.Models;

namespace MailArchiver.Services.Providers
{
    /// <summary>
    /// Service interface for Microsoft Graph email operations
    /// </summary>
    public interface IGraphEmailService
    {
        /// <summary>
        /// Syncs emails from Microsoft Graph API for M365 accounts
        /// </summary>
        /// <param name="account">The M365 mail account</param>
        /// <param name="jobId">Optional sync job ID for progress tracking</param>
        /// <returns>Task</returns>
        Task SyncMailAccountAsync(MailAccount account, string? jobId = null);

        /// <summary>
        /// Tests the connection to Microsoft Graph API
        /// </summary>
        /// <param name="account">The M365 mail account</param>
        /// <returns>True if connection is successful</returns>
        Task<bool> TestConnectionAsync(MailAccount account);

        /// <summary>
        /// Gets mail folders from Microsoft Graph API
        /// </summary>
        /// <param name="account">The M365 mail account</param>
        /// <returns>List of folder names</returns>
        Task<List<string>> GetMailFoldersAsync(MailAccount account);

        /// <summary>
        /// Restores an email to a specific folder using Microsoft Graph API
        /// </summary>
        /// <param name="email">The archived email to restore</param>
        /// <param name="targetAccount">The target M365 account</param>
        /// <param name="folderName">The target folder name</param>
        /// <returns>True if restoration is successful</returns>
        Task<bool> RestoreEmailToFolderAsync(ArchivedEmail email, MailAccount targetAccount, string folderName);
    }
}

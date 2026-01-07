using MailArchiver.Models;

namespace MailArchiver.Services.Providers
{
    /// <summary>
    /// Common interface for all email provider services (IMAP, M365, Import)
    /// </summary>
    public interface IProviderEmailService
    {
        /// <summary>
        /// Syncs emails from the provider for the specified account
        /// </summary>
        /// <param name="account">The mail account to sync</param>
        /// <param name="jobId">Optional sync job ID for progress tracking</param>
        /// <returns>Task</returns>
        Task SyncMailAccountAsync(MailAccount account, string? jobId = null);

        /// <summary>
        /// Tests the connection to the email provider
        /// </summary>
        /// <param name="account">The mail account to test</param>
        /// <returns>True if connection is successful</returns>
        Task<bool> TestConnectionAsync(MailAccount account);

        /// <summary>
        /// Gets mail folders from the email provider
        /// </summary>
        /// <param name="accountId">The mail account ID</param>
        /// <returns>List of folder names</returns>
        Task<List<string>> GetMailFoldersAsync(int accountId);

        /// <summary>
        /// Restores a single email to a specific folder
        /// </summary>
        /// <param name="emailId">The archived email ID to restore</param>
        /// <param name="targetAccountId">The target account ID</param>
        /// <param name="folderName">The target folder name</param>
        /// <returns>True if restoration is successful</returns>
        Task<bool> RestoreEmailToFolderAsync(int emailId, int targetAccountId, string folderName);

        /// <summary>
        /// Restores multiple emails to a specific folder with progress tracking
        /// </summary>
        /// <param name="emailIds">List of email IDs to restore</param>
        /// <param name="targetAccountId">The target account ID</param>
        /// <param name="folderName">The target folder name</param>
        /// <param name="progressCallback">Progress callback (total, successful, failed)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Tuple with successful and failed counts</returns>
        Task<(int Successful, int Failed)> RestoreMultipleEmailsWithProgressAsync(
            List<int> emailIds,
            int targetAccountId,
            string folderName,
            Action<int, int, int> progressCallback,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a full resync of an account
        /// </summary>
        /// <param name="accountId">The account ID to resync</param>
        /// <returns>True if resync was successful</returns>
        Task<bool> ResyncAccountAsync(int accountId);

        /// <summary>
        /// Gets the email count for a specific account
        /// </summary>
        /// <param name="accountId">The account ID</param>
        /// <returns>Email count</returns>
        Task<int> GetEmailCountByAccountAsync(int accountId);
    }
}

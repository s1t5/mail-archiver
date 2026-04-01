using MailArchiver.Models;

namespace MailArchiver.Services
{
    /// <summary>
    /// Interface for bandwidth tracking and rate limit management.
    /// Used to track IMAP bandwidth usage and handle provider rate limits.
    /// </summary>
    public interface IBandwidthService
    {
        /// <summary>
        /// Checks if the bandwidth limit has been reached for the specified account.
        /// </summary>
        /// <param name="accountId">The mail account ID to check.</param>
        /// <returns>True if the limit has been reached and sync should pause.</returns>
        Task<bool> IsLimitReachedAsync(int accountId);

        /// <summary>
        /// Gets the current bandwidth status for the specified account.
        /// </summary>
        /// <param name="accountId">The mail account ID.</param>
        /// <returns>A tuple containing bytes used, limit, and whether limit is reached.</returns>
        Task<BandwidthStatus> GetStatusAsync(int accountId);

        /// <summary>
        /// Tracks bandwidth usage for the specified account.
        /// Uses atomic DB update to prevent lost updates under concurrent access.
        /// </summary>
        /// <param name="accountId">The mail account ID.</param>
        /// <param name="bytesDownloaded">Number of bytes downloaded.</param>
        /// <param name="bytesUploaded">Number of bytes uploaded (if tracking enabled).</param>
        /// <param name="emailsProcessed">Number of emails processed.</param>
        /// <returns>The updated bandwidth usage record.</returns>
        Task<BandwidthUsage> TrackUsageAsync(int accountId, long bytesDownloaded, long bytesUploaded = 0, int emailsProcessed = 1);

        /// <summary>
        /// Tracks bandwidth usage and checks if the limit has been reached in a single operation.
        /// Combines TrackUsageAsync + IsLimitReachedAsync to reduce DB roundtrips.
        /// </summary>
        /// <param name="accountId">The mail account ID.</param>
        /// <param name="bytesDownloaded">Number of bytes downloaded.</param>
        /// <param name="bytesUploaded">Number of bytes uploaded (if tracking enabled).</param>
        /// <param name="emailsProcessed">Number of emails processed.</param>
        /// <returns>A tuple: the updated usage record and whether the limit has been reached.</returns>
        Task<(BandwidthUsage Usage, bool LimitReached)> TrackUsageAndCheckLimitAsync(int accountId, long bytesDownloaded, long bytesUploaded = 0, int emailsProcessed = 1);

        /// <summary>
        /// Marks the limit as reached for the specified account.
        /// </summary>
        /// <param name="accountId">The mail account ID.</param>
        /// <param name="resetTime">When the limit will reset (null for default hours).</param>
        Task SetLimitReachedAsync(int accountId, DateTime? resetTime = null);

        /// <summary>
        /// Clears the limit reached flag (e.g., after daily reset).
        /// </summary>
        /// <param name="accountId">The mail account ID.</param>
        Task ClearLimitReachedAsync(int accountId);

        /// <summary>
        /// Gets or creates a sync checkpoint for the specified account and folder.
        /// </summary>
        /// <param name="accountId">The mail account ID.</param>
        /// <param name="folderName">The folder name.</param>
        /// <returns>The existing or new checkpoint.</returns>
        Task<SyncCheckpoint> GetOrCreateCheckpointAsync(int accountId, string folderName);

        /// <summary>
        /// Updates the checkpoint with the last processed message info.
        /// </summary>
        /// <param name="accountId">The mail account ID.</param>
        /// <param name="folderName">The folder name.</param>
        /// <param name="lastMessageDate">Date of the last processed message.</param>
        /// <param name="lastMessageId">Message-ID of the last processed message.</param>
        /// <param name="bytesDownloaded">Bytes downloaded for this message.</param>
        Task UpdateCheckpointAsync(int accountId, string folderName, DateTime? lastMessageDate, string? lastMessageId, long bytesDownloaded = 0);

        /// <summary>
        /// Marks a folder checkpoint as completed.
        /// </summary>
        /// <param name="accountId">The mail account ID.</param>
        /// <param name="folderName">The folder name.</param>
        Task MarkFolderCompletedAsync(int accountId, string folderName);

        /// <summary>
        /// Clears all checkpoints for the specified account (after successful sync).
        /// </summary>
        /// <param name="accountId">The mail account ID.</param>
        Task ClearCheckpointsAsync(int accountId);

        /// <summary>
        /// Gets all checkpoints for the specified account.
        /// </summary>
        /// <param name="accountId">The mail account ID.</param>
        /// <returns>List of checkpoints for the account.</returns>
        Task<List<SyncCheckpoint>> GetCheckpointsAsync(int accountId);

        /// <summary>
        /// Checks if there are any incomplete checkpoints (indicating interrupted sync).
        /// </summary>
        /// <param name="accountId">The mail account ID.</param>
        /// <returns>True if there are incomplete checkpoints.</returns>
        Task<bool> HasIncompleteCheckpointsAsync(int accountId);

        /// <summary>
        /// Cleans up old bandwidth usage records that are no longer needed.
        /// This should be called periodically to prevent unlimited growth of the BandwidthUsage table.
        /// </summary>
        /// <param name="olderThanDays">Remove records older than this many days</param>
        /// <returns>Number of records removed</returns>
        Task<int> CleanupOldBandwidthRecordsAsync(int olderThanDays = 7);

        /// <summary>
        /// Cleans up old sync checkpoints that are no longer needed.
        /// This should be called after successful syncs or periodically for orphaned checkpoints.
        /// </summary>
        /// <param name="olderThanDays">Remove records older than this many days</param>
        /// <returns>Number of records removed</returns>
        Task<int> CleanupOldCheckpointsAsync(int olderThanDays = 30);
    }

    /// <summary>
    /// Represents the bandwidth status for an account.
    /// </summary>
    public class BandwidthStatus
    {
        /// <summary>
        /// Number of bytes downloaded today.
        /// </summary>
        public long BytesDownloaded { get; set; }

        /// <summary>
        /// Number of bytes uploaded today (if tracking enabled).
        /// </summary>
        public long BytesUploaded { get; set; }

        /// <summary>
        /// Number of emails processed today.
        /// </summary>
        public int EmailsProcessed { get; set; }

        /// <summary>
        /// Daily download limit in bytes.
        /// </summary>
        public long DailyLimitBytes { get; set; }

        /// <summary>
        /// Percentage of limit used (0-100).
        /// </summary>
        public double PercentUsed { get; set; }

        /// <summary>
        /// Whether the limit has been reached.
        /// </summary>
        public bool LimitReached { get; set; }

        /// <summary>
        /// When the limit will reset (if reached).
        /// </summary>
        public DateTime? ResetTime { get; set; }

        /// <summary>
        /// Whether bandwidth tracking is enabled.
        /// </summary>
        public bool TrackingEnabled { get; set; }
    }
}
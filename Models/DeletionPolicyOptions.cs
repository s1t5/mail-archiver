namespace MailArchiver.Models
{
    /// <summary>
    /// Configuration options for the email deletion policy.
    /// When <see cref="DeletionAllowed"/> is false, manual deletion of archived
    /// emails is blocked on the application level and all archived emails are
    /// locked via the database compliance trigger (IsLocked = true) on startup.
    /// Local retention deletion is exempt and still runs.
    /// </summary>
    public class DeletionPolicyOptions
    {
        public const string DeletionPolicy = "DeletionPolicy";

        /// <summary>
        /// Whether manual deletion of archived emails is allowed.
        /// Default: true (deletion allowed)
        /// </summary>
        public bool DeletionAllowed { get; set; } = true;

        /// <summary>
        /// Upper bound for one filter-based bulk deletion job. A filter that matches
        /// more emails is rejected instead of silently truncated, so the user has to
        /// narrow the filter and confirm the smaller set.
        /// Default: 100000
        /// </summary>
        public int MaxBulkDeleteEmails { get; set; } = 100_000;
    }
}
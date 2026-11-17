namespace MailArchiver.Models
{
    /// <summary>
    /// Settings for the authenticated health endpoints under /api/v1/health.
    /// </summary>
    public class HealthOptions
    {
        public const string Health = "Health";

        /// <summary>
        /// Enabled accounts whose last sync is older than this many minutes make
        /// /api/v1/health report Degraded. 0 disables the check.
        /// </summary>
        public int SyncStaleMinutes { get; set; } = 0;
    }
}

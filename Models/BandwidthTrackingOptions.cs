namespace MailArchiver.Models
{
    /// <summary>
    /// Configuration options for bandwidth tracking and rate limit handling.
    /// Used to prevent IMAP rate limits from providers like Gmail (2500MB/day).
    /// </summary>
    public class BandwidthTrackingOptions
    {
        public const string BandwidthTracking = "BandwidthTracking";

        /// <summary>
        /// Enable or disable bandwidth tracking.
        /// When disabled, no bandwidth limits are enforced.
        /// Default: false (disabled)
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Daily download limit in megabytes.
        /// Gmail limit: 2500 MB per 24 hours.
        /// Default: 25000 MB (25 GB) for generous default
        /// </summary>
        public long DailyLimitMb { get; set; } = 25000;

        /// <summary>
        /// Percentage of the daily limit at which to start warning.
        /// Default: 80 (warn at 80% of limit)
        /// </summary>
        public int WarningThresholdPercent { get; set; } = 80;

        /// <summary>
        /// Hours to pause sync when limit is reached.
        /// Default: 24 hours (typical for Gmail reset)
        /// </summary>
        public int PauseHoursOnLimit { get; set; } = 24;

        /// <summary>
        /// Track upload bandwidth in addition to download.
        /// Default: false (most providers limit download only)
        /// </summary>
        public bool TrackUploadBytes { get; set; } = false;
    }
}
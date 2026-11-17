namespace MailArchiver.Models
{
    /// <summary>
    /// Settings for the Prometheus-compatible metrics endpoint under /api/v1/metrics.
    /// </summary>
    public class MetricsOptions
    {
        public const string Metrics = "Metrics";

        /// <summary>
        /// Enables the metrics endpoint and its background snapshot service. Disabled by
        /// default because the counts scan the archive and are not needed on every install.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// How often the snapshot is rebuilt. Values below one minute are raised to one.
        /// </summary>
        public int RefreshMinutes { get; set; } = 5;
    }
}

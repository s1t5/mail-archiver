// Models/DashboardOptions.cs
namespace MailArchiver.Models
{
    public class DashboardOptions
    {
        public const string SectionName = "Dashboard";

        /// <summary>
        /// How long computed dashboard statistics are kept in the in-memory cache.
        /// 0 disables caching (statistics are recomputed on every request).
        /// </summary>
        public int CacheSeconds { get; set; } = 60;

        /// <summary>
        /// How often the DashboardStatsRefreshService recomputes the expensive
        /// dashboard aggregates (totals, top senders, monthly histogram, account
        /// counts) into the DashboardStatsCache table. On large installations the
        /// dashboard reads those prepared values instead of aggregating millions
        /// of rows in the request path. 0 disables the background refresh and
        /// makes the dashboard compute everything live again.
        /// </summary>
        public int RefreshIntervalMinutes { get; set; } = 15;
    }
}
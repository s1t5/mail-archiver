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
    }
}
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
        /// Whether the counter cards carry their incoming and outgoing parts, and the account
        /// card the number of domains.
        /// <para>
        /// Turning this off does not hide the numbers, it stops computing them: the counters go
        /// back to a plain count, and the attachment count stops joining to the mail it hangs
        /// on, which is the one query these parts add.
        /// </para>
        /// </summary>
        public bool ShowDirectionSplits { get; set; } = true;

        /// <summary>
        /// Whether the charts offer a resolution, a period and arrows to move it.
        /// <para>
        /// Turning this off leaves the twelve month histogram and the senders of the whole
        /// archive, which is what the dashboard showed before the pickers existed. The chart
        /// endpoint then answers as if it did not exist, so a hand made request cannot drive the
        /// queries behind it either.
        /// </para>
        /// </summary>
        public bool SelectablePeriods { get; set; } = true;
    }
}
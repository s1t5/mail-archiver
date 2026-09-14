namespace MailArchiver.Models
{
    /// <summary>
    /// Configuration options for the background jobs page.
    /// </summary>
    public class JobsOptions
    {
        public const string SectionName = "Jobs";

        /// <summary>
        /// How often the jobs page reloads itself while it is open. Every reload rebuilds the
        /// page from all eight job sources, so a short interval on an installation with many
        /// accounts is a real load, multiplied by every open tab.
        /// 0 turns the automatic reload off.
        /// </summary>
        public int RefreshSeconds { get; set; } = 30;
    }
}

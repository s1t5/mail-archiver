namespace MailArchiver.Models
{
    /// <summary>
    /// Configuration options for the release notes / version update splash screen.
    /// The GitHub repository (s1t5/mail-archiver) is hardcoded in VersionUpdateService.
    /// </summary>
    public class ReleaseNotesOptions
    {
        public const string ReleaseNotes = "ReleaseNotes";

        /// <summary>
        /// Enable or disable the version update splash screen.
        /// Default: true
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// HTTP request timeout in seconds for GitHub API calls.
        /// Default: 10
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Application version string used to match against GitHub release tags.
        /// When empty, the version will be read from the compiled assembly.
        /// The update hint requires this value; without it no hint is shown.
        /// </summary>
        public string? AppVersion { get; set; }

        /// <summary>
        /// Show administrators a small hint when a newer stable release exists on GitHub.
        /// Default: true
        /// </summary>
        public bool UpdateHintEnabled { get; set; } = true;

        /// <summary>
        /// How often the GitHub release list is refreshed, in hours.
        /// Also controls how soon a new release becomes visible in a long-running instance.
        /// Default: 6
        /// </summary>
        public int UpdateCheckIntervalHours { get; set; } = 6;
    }
}
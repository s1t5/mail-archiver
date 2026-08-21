// Models/OffloadOptions.cs
namespace MailArchiver.Models
{
    /// <summary>
    /// Settings for the date-windowed offload of archived mail into another mailbox.
    /// Every default reproduces the behaviour the application had before the feature
    /// existed, so an installation that does not configure this section is unaffected.
    /// </summary>
    public class OffloadOptions
    {
        public const string Offload = "Offload";

        /// <summary>
        /// How many offload jobs may run at the same time. One keeps the strictly serial
        /// processing the batch restore service has always used.
        /// </summary>
        public int MaxConcurrentJobs { get; set; } = 1;

        /// <summary>
        /// Upper bound on the number of messages indexed from a target mailbox before the
        /// duplicate check narrows its scope from the whole mailbox to a single folder.
        /// Guards memory on an unexpectedly large target.
        /// </summary>
        public int PrefetchMaxMessages { get; set; } = 500_000;

        /// <summary>
        /// Source folders that are never offloaded, matched before any rename is applied and
        /// covering their descendants. Empty by default: rewriting or dropping folders without
        /// being asked to would surprise existing users.
        /// </summary>
        public List<string> ExcludedSourceFolders { get; set; } = new();

        /// <summary>
        /// Rewrites the leading path segments of a source folder, for example
        /// "Sent Items" to "Sent". Empty by default, for the same reason as above.
        /// </summary>
        public Dictionary<string, string> FolderRenameMap { get; set; } = new();

        /// <summary>
        /// Whether appended mail is flagged as read. True reproduces the behaviour of the
        /// existing restore path, which always appended with <c>MessageFlags.Seen</c>.
        /// </summary>
        public bool MarkAsSeen { get; set; } = true;
    }
}

// Models/OffloadCriteria.cs
namespace MailArchiver.Models
{
    /// <summary>
    /// What an offload job selects and how it maps folders, carried instead of a materialised
    /// list of email ids.
    /// <para>
    /// The worker resolves the ids itself from these criteria. That removes the need to push up
    /// to fifty thousand ids through session state, and it turns the batch restore ceiling into
    /// a sanity check on the resolved count rather than the binding constraint it is today.
    /// </para>
    /// <para>
    /// The cutoffs are absolute. A relative expression such as "the last six months" is resolved
    /// once, when the job is created, because a job may run for a long time and be repeated
    /// afterwards and has to select the same mail every time.
    /// </para>
    /// </summary>
    public class OffloadCriteria
    {
        public int SourceAccountId { get; set; }

        /// <summary>
        /// Inclusive lower bound on <c>SentDate</c>, interpreted in the configured display
        /// timezone because that is the timezone the column is stored in.
        /// </summary>
        public DateTime CutoffFrom { get; set; }

        /// <summary>
        /// Optional upper bound. The whole of this day is inside the window, matching the
        /// semantics of the existing search filter.
        /// </summary>
        public DateTime? CutoffTo { get; set; }

        /// <summary>Source folders never offloaded. Matched before any rename is applied.</summary>
        public List<string> ExcludedSourceFolders { get; set; } = new();

        /// <summary>Rewrites the leading path segments of a source folder.</summary>
        public Dictionary<string, string> FolderRenameMap { get; set; } = new();

        /// <summary>Whether appended mail is flagged as read.</summary>
        public bool MarkAsSeen { get; set; } = true;

        /// <summary>
        /// Resolves everything and reports what would happen, but appends nothing.
        /// </summary>
        public bool DryRun { get; set; }

        /// <summary>Human readable description of the window, for logs and the job list.</summary>
        public string DescribeWindow()
            => CutoffTo.HasValue
                ? $"{CutoffFrom:yyyy-MM-dd} .. {CutoffTo.Value:yyyy-MM-dd}"
                : $"from {CutoffFrom:yyyy-MM-dd}";
    }
}

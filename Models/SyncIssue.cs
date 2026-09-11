namespace MailArchiver.Models
{
    /// <summary>
    /// What kind of trouble a single entry in a sync job's issue log describes.
    ///
    /// Three, on purpose. The kind exists to group the list in the UI and to cap each group
    /// separately, so a thousand failed messages cannot push sixteen folder problems out of view.
    /// It is not a classification of causes — the cause is text, in <see cref="SyncIssue.Reason"/>.
    ///
    /// Rate limits and timeouts are deliberately absent: those describe the whole run, are already
    /// carried by <see cref="SyncJobStatus"/>, and would produce exactly one list entry per job.
    /// Recovered messages and provider placeholders are absent too — they are not problems, and one
    /// real account produced 8318 of them in a single run, which would exhaust any cap and bury the
    /// entries that matter.
    /// </summary>
    public enum SyncIssueKind
    {
        /// <summary>The folder could not be opened or searched. Holds LastSync back.</summary>
        FolderFailed,

        /// <summary>
        /// Discovery reported the folder and the server then said it does not exist. Does not hold
        /// LastSync back: nothing can be retried and nothing will change on a later run.
        /// </summary>
        FolderMissing,

        /// <summary>A single message could not be archived. Holds LastSync back.</summary>
        MessageFailed
    }

    /// <summary>
    /// One thing that went wrong during a sync, in a form the account page can render without the
    /// reader knowing what EXAMINE or a UID is.
    /// </summary>
    public class SyncIssue
    {
        public SyncIssueKind Kind { get; init; }

        /// <summary>Folder the trouble happened in. Always set.</summary>
        public string Folder { get; init; } = string.Empty;

        /// <summary>IMAP UID, for <see cref="SyncIssueKind.MessageFailed"/> where it is known.</summary>
        public uint? Uid { get; init; }

        /// <summary>Subject of the message, where one could be read.</summary>
        public string? Subject { get; init; }

        /// <summary>
        /// Innermost cause, as the server or the library phrased it. Raw on purpose: it is the part
        /// an operator pastes into a search, and softening it would lose the only precise wording
        /// there is.
        /// </summary>
        public string Reason { get; init; } = string.Empty;

        public DateTime AtUtc { get; init; } = DateTime.UtcNow;
    }
}

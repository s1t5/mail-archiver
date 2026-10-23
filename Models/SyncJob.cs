namespace MailArchiver.Models
{
    public class SyncJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public int MailAccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public DateTime Started { get; set; } = DateTime.UtcNow;
        public DateTime? Completed { get; set; }
        public SyncJobStatus Status { get; set; } = SyncJobStatus.Running;
        public int ProcessedFolders { get; set; }
        public int TotalFolders { get; set; }
        public int ProcessedEmails { get; set; }
        public int NewEmails { get; set; }
        public int FailedEmails { get; set; }

        /// <summary>
        /// Folders that failed as a unit. Counted per folder, never converted into a number of
        /// failed messages, and it holds LastSync back exactly as failed messages do.
        /// </summary>
        public int FailedFolders { get; set; }

        /// <summary>
        /// Folders discovery reported and the server then said do not exist. Does not hold LastSync
        /// back, but worth showing: it usually means the mailbox carries subscriptions to folders
        /// that were deleted long ago.
        /// </summary>
        public int MissingFolders { get; set; }

        /// <summary>
        /// What went wrong, in detail and bounded per kind. The counters above say how much; this
        /// says what, which is the part that was previously only in the log file.
        /// </summary>
        public MailArchiver.Services.Shared.SyncIssueLog Issues { get; set; } =
            new MailArchiver.Services.Shared.SyncIssueLog(0);
        public int DeletedEmails { get; set; } // New property to track deleted emails
        public string? CurrentFolder { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime? LastSync { get; set; }
        public bool FailuresAcknowledged { get; set; }
        public CancellationTokenSource? CancellationTokenSource { get; set; }
        /// <summary>Display name of the user who started the job; "System" for operator/background starts (P2).</summary>
        public string UserId { get; set; } = "System";
    }

    public enum SyncJobStatus
    {
        Running,
        Completed,
        Failed,
        Cancelled,
        RateLimited,
        TimedOut
    }
}

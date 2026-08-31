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
        RateLimited
    }
}

namespace MailArchiver.Models
{
    public class PstImportJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int TargetAccountId { get; set; }
        public string TargetFolder { get; set; } = "INBOX";
        public bool PreserveFolderStructure { get; set; } = true;
        public string UserId { get; set; } = "System";
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime? Started { get; set; }
        public DateTime? Completed { get; set; }
        public PstImportJobStatus Status { get; set; } = PstImportJobStatus.Queued;
        public int ProcessedEmails { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedAlreadyExistsCount { get; set; }
        public int TotalEmails { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CurrentEmailSubject { get; set; }
        public string? CurrentFolder { get; set; }
    }

    public enum PstImportJobStatus
    {
        Queued,
        Running,
        Completed,
        CompletedWithErrors,
        Failed,
        Cancelled
    }
}

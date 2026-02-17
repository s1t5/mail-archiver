namespace MailArchiver.Models
{
    public class MBoxImportJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int TargetAccountId { get; set; }
        public string TargetFolder { get; set; } = "INBOX";
        public string UserId { get; set; } = "System";
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime? Started { get; set; }
        public DateTime? Completed { get; set; }
        public MBoxImportJobStatus Status { get; set; } = MBoxImportJobStatus.Queued;
        public int ProcessedEmails { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int SkippedMalformedCount { get; set; }
        public int TotalEmails { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CurrentEmailSubject { get; set; }
        public long ProcessedBytes { get; set; }
    }

    public enum MBoxImportJobStatus
    {
        Queued,
        Running,
        Completed,
        CompletedWithErrors,
        Failed,
        Cancelled
    }
}
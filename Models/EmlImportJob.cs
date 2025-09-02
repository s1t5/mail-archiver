namespace MailArchiver.Models
{
    public class EmlImportJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int TargetAccountId { get; set; }
        public string UserId { get; set; } = "System";
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime? Started { get; set; }
        public DateTime? Completed { get; set; }
        public EmlImportJobStatus Status { get; set; } = EmlImportJobStatus.Queued;
        public int ProcessedEmails { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public int TotalEmails { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CurrentEmailSubject { get; set; }
        public long ProcessedBytes { get; set; }
    }

    public enum EmlImportJobStatus
    {
        Queued,
        Running,
        Completed,
        Failed,
        Cancelled
    }
}

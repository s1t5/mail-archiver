// Models/BatchRestoreJob.cs
namespace MailArchiver.Models
{
    public class BatchRestoreJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public List<int> EmailIds { get; set; } = new List<int>();
        public int TargetAccountId { get; set; }
        public string TargetFolder { get; set; } = "INBOX";
        public string UserId { get; set; } = "System";
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime? Started { get; set; }
        public DateTime? Completed { get; set; }
        public BatchRestoreJobStatus Status { get; set; } = BatchRestoreJobStatus.Queued;
        public int ProcessedCount { get; set; }
        public int SuccessCount { get; set; }
        public int FailedCount { get; set; }
        public string? ErrorMessage { get; set; }
        public string ReturnUrl { get; set; } = "";
    }

    public enum BatchRestoreJobStatus
    {
        Queued,
        Running,
        Completed,
        Failed,
        Cancelled
    }
}
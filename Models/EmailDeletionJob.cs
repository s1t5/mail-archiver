using System;
using System.Collections.Generic;

namespace MailArchiver.Models
{
    public enum EmailDeletionJobStatus
    {
        Queued,
        Running,
        Completed,
        Failed,
        Cancelled
    }

    /// <summary>
    /// Selection criteria for a deletion job that targets a whole folder (optionally
    /// narrowed by the same filters used in the email list search) instead of an
    /// explicit list of email IDs. The matching IDs are resolved when the job runs.
    /// </summary>
    public class EmailDeletionCriteria
    {
        public int AccountId { get; set; }
        public string FolderName { get; set; } = string.Empty;
        public string? SearchTerm { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public bool? IsOutgoing { get; set; }
    }

    public class EmailDeletionJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public List<int> EmailIds { get; set; } = new List<int>();
        public EmailDeletionCriteria? Criteria { get; set; }
        public int TotalEmails { get; set; }
        public int DeletedEmails { get; set; }
        public int TotalAttachments { get; set; }
        public int DeletedAttachments { get; set; }
        public EmailDeletionJobStatus Status { get; set; } = EmailDeletionJobStatus.Queued;
        public string CurrentPhase { get; set; } = "Initializing";
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime? Started { get; set; }
        public DateTime? Completed { get; set; }
        public string? ErrorMessage { get; set; }
        public string UserId { get; set; } = "System";
        public bool IsCompleted => Status == EmailDeletionJobStatus.Completed || 
                                   Status == EmailDeletionJobStatus.Failed || 
                                   Status == EmailDeletionJobStatus.Cancelled;
        public CancellationTokenSource CancellationTokenSource { get; set; } = new CancellationTokenSource();
    }
}

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

    public class EmailDeletionJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public List<int> EmailIds { get; set; } = new List<int>();
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

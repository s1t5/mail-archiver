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

        /// <summary>Explicit selection. Empty when the job is filter-based.</summary>
        public List<int> EmailIds { get; set; } = new List<int>();

        /// <summary>Filter criteria. When set, the job resolves the matching ids batch by batch.</summary>
        public EmailDeletionFilter? Filter { get; set; }

        /// <summary>Keyset cursor for filter-based jobs: highest id already processed.</summary>
        public int LastProcessedId { get; set; }

        /// <summary>Matching emails that were skipped because the retention policy locked them.</summary>
        public int SkippedLockedEmails { get; set; }

        public bool IsFilterJob => Filter != null;

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

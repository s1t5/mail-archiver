// Models/BatchRestoreJob.cs
namespace MailArchiver.Models
{
    public class BatchRestoreJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public List<int> EmailIds { get; set; } = new List<int>();
        public int TargetAccountId { get; set; }
        public string TargetFolder { get; set; } = "INBOX";
        
        /// <summary>
        /// If true, preserves the original folder structure from the archived emails
        /// by recreating the relative folder hierarchy under the target folder.
        /// </summary>
        public bool PreserveFolderStructure { get; set; } = false;
        
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

        /// <summary>
        /// When set, the job is a date-windowed offload and resolves its own set of mail from
        /// these criteria instead of carrying a materialised list in <see cref="EmailIds"/>.
        /// Null keeps the existing behaviour exactly, so the checkbox selection path is
        /// unaffected.
        /// </summary>
        public OffloadCriteria? Offload { get; set; }

        public bool IsOffload => Offload != null;

        // Offload counters, using the vocabulary of the import job so that repeating a finished
        // job reads as a verification pass: nothing appended, everything already present.
        public int AppendedCount { get; set; }
        public int SkippedAlreadyPresentCount { get; set; }
        public int MatchedByFingerprintCount { get; set; }
        public int SkippedExcludedFolderCount { get; set; }

        /// <summary>Per-folder report, shown for a dry run where the button is.</summary>
        public string? Report { get; set; }
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
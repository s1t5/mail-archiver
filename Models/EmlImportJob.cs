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
        public int SkippedAlreadyExistsCount { get; set; }
        public int TotalEmails { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CurrentEmailSubject { get; set; }
        public long ProcessedBytes { get; set; }
        /// <summary>
        /// Emails that were recovered via tolerant header parsing
        /// (mbox From-line, banner line, junk lines, split header block).
        /// </summary>
        public int RecoveredCount { get; set; }
        /// <summary>
        /// Files skipped during the import, with a categorized reason.
        /// In-memory only (job lifetime is capped by job cleanup); capped at
        /// <see cref="MaxFailedEntries"/> to bound memory usage.
        /// </summary>
        public List<EmlImportFailedEntry> FailedEntries { get; set; } = new();
        /// <summary>Maximum number of failed entries kept per job.</summary>
        public const int MaxFailedEntries = 200;
        /// <summary>
        /// If true, the source file will not be deleted after processing (for CLI local imports).
        /// </summary>
        public bool KeepSourceFile { get; set; }
    }

    /// <summary>
    /// A single file that could not be imported, with a user-facing reason.
    /// </summary>
    public class EmlImportFailedEntry
    {
        /// <summary>Path of the entry inside the ZIP archive (or file name).</summary>
        public string EntryPath { get; set; } = string.Empty;
        /// <summary>Localization key suffix of the reason (e.g. "BannerLine", "Unparseable").</summary>
        public string Reason { get; set; } = string.Empty;
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

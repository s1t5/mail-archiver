using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models
{
    /// <summary>
    /// Persisted history row for one audit data export run. The job lifecycle
    /// (Queued → Running → Completed/Failed/Cancelled/Downloaded) is tracked in
    /// the database so the export history is revision-safe across restarts.
    /// </summary>
    public class AuditExportJob
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Username { get; set; } = string.Empty;

        public DateTime Created { get; set; } = DateTime.UtcNow;

        public DateTime? Started { get; set; }

        public DateTime? Completed { get; set; }

        public AuditExportJobStatus Status { get; set; } = AuditExportJobStatus.Queued;

        public DateTime FromDate { get; set; }

        public DateTime ToDate { get; set; }

        public int? MailAccountId { get; set; }

        public string? MailAccountName { get; set; }

        public bool IncludeAttachments { get; set; }

        public string? DataSupplierName { get; set; }

        public string? DataSupplierLocation { get; set; }

        public string? DataSupplierComment { get; set; }

        public int TotalEmails { get; set; }

        public int ProcessedEmails { get; set; }

        public string? OutputFilePath { get; set; }

        public long OutputFileSize { get; set; }

        [MaxLength(2000)]
        public string? ErrorMessage { get; set; }
    }

    public enum AuditExportJobStatus
    {
        Queued,
        Running,
        Completed,
        Failed,
        Cancelled,
        Downloaded
    }
}
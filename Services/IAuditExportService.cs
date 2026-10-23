using MailArchiver.Models;

namespace MailArchiver.Services
{
    public interface IAuditExportService
    {
        Task<AuditExportJob> StartJobAsync(AuditExportRequest request, string username);
        Task<AuditExportJob?> GetJobAsync(Guid jobId);
        Task<List<AuditExportJob>> GetRecentJobsAsync(int limit = 20);
        Task<bool> CancelJobAsync(Guid jobId);
        Task<AuditExportFileResult?> GetExportForDownloadAsync(Guid jobId);
        Task MarkAsDownloadedAsync(Guid jobId);
        void CleanupOldExportFiles();
    }

    public class AuditExportRequest
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int? MailAccountId { get; set; }
        public bool IncludeAttachments { get; set; }
        public string? DataSupplierName { get; set; }
        public string? DataSupplierLocation { get; set; }
        public string? DataSupplierComment { get; set; }
    }

    public class AuditExportFileResult
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/zip";
    }
}
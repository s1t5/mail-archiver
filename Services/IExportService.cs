using MailArchiver.Models;

namespace MailArchiver.Services
{
    public interface IExportService
    {
        string QueueExport(int mailAccountId, AccountExportFormat format, string userId);
        AccountExportJob? GetJob(string jobId);
        List<AccountExportJob> GetActiveJobs();
        List<AccountExportJob> GetAllJobs();
        bool CancelJob(string jobId);
        FileResult? GetExportForDownload(string jobId);
        bool MarkAsDownloaded(string jobId);
    }

    public class FileResult
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
    }
}

using MailArchiver.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MailArchiver.Services
{
    public interface ISelectedEmailsExportService
    {
        string QueueExport(List<int> emailIds, AccountExportFormat format, string userId);
        SelectedEmailsExportJob? GetJob(string jobId);
        List<SelectedEmailsExportJob> GetActiveJobs();
        List<SelectedEmailsExportJob> GetAllJobs();
        bool CancelJob(string jobId);
        Task<FileResult?> DownloadExportAsync(string jobId);
        bool MarkAsDownloaded(string jobId);
    }
}

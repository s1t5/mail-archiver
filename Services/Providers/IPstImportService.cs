using MailArchiver.Models;

namespace MailArchiver.Services
{
    public interface IPstImportService
    {
        string QueueImport(PstImportJob job);
        PstImportJob? GetJob(string jobId);
        List<PstImportJob> GetActiveJobs();
        List<PstImportJob> GetAllJobs();
        bool CancelJob(string jobId);
        Task<string> SaveUploadedFileAsync(IFormFile file);
        void CleanupOldJobs();
    }
}

using MailArchiver.Models;

namespace MailArchiver.Services
{
    public interface IEmlImportService
    {
        string QueueImport(EmlImportJob job);
        EmlImportJob? GetJob(string jobId);
        List<EmlImportJob> GetActiveJobs();
        bool CancelJob(string jobId);
        Task<string> SaveUploadedFileAsync(IFormFile file);
        Task<int> EstimateEmailCountAsync(string filePath);
        void CleanupOldJobs();
    }
}

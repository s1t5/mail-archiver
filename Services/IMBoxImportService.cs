using MailArchiver.Models;

namespace MailArchiver.Services
{
    public interface IMBoxImportService
    {
        string QueueImport(MBoxImportJob job);
        MBoxImportJob? GetJob(string jobId);
        List<MBoxImportJob> GetActiveJobs();
        bool CancelJob(string jobId);
        void CleanupOldJobs();
        Task<string> SaveUploadedFileAsync(IFormFile file);
        Task<int> EstimateEmailCountAsync(string filePath);
    }
}
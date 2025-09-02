using MailArchiver.Models;

namespace MailArchiver.Services
{
    /// <summary>
    /// Interface for MBox import service (also used for EML imports)
    /// </summary>
    public interface IMBoxImportService
    {
        string QueueImport(MBoxImportJob job);
        MBoxImportJob? GetJob(string jobId);
        List<MBoxImportJob> GetActiveJobs();
        bool CancelJob(string jobId);
        Task<string> SaveUploadedFileAsync(IFormFile file);
        Task<int> EstimateEmailCountAsync(string filePath);
        void CleanupOldJobs();
    }
}

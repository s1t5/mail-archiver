using MailArchiver.Models;

namespace MailArchiver.Services
{
    public interface IEmlImportService
    {
        string QueueImport(EmlImportJob job);
        EmlImportJob? GetJob(string jobId);
        List<EmlImportJob> GetActiveJobs();
        List<EmlImportJob> GetAllJobs();
        bool CancelJob(string jobId);
        Task<string> SaveUploadedFileAsync(IFormFile file);
        Task<int> EstimateEmailCountAsync(string filePath);
        void CleanupOldJobs();
        /// <summary>
        /// Process a local file directly (for CLI imports). Returns the job result after completion.
        /// </summary>
        Task<EmlImportJob> ProcessFileAsync(string filePath, string fileName, int targetAccountId, string userId, CancellationToken cancellationToken = default);
    }
}

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
        List<MBoxImportJob> GetAllJobs();
        bool CancelJob(string jobId);
        Task<string> SaveUploadedFileAsync(IFormFile file);
        Task<int> EstimateEmailCountAsync(string filePath);
        void CleanupOldJobs();
        /// <summary>
        /// Process a local file directly (for CLI imports). Returns the job result after completion.
        /// </summary>
        Task<MBoxImportJob> ProcessFileAsync(string filePath, string fileName, int targetAccountId, string targetFolder, string userId, CancellationToken cancellationToken = default);
    }
}

using MailArchiver.Models;

namespace MailArchiver.Services.Providers
{
    /// <summary>
    /// Import email provider service (EML/MBOX imports)
    /// Currently placeholder - implementation pending
    /// </summary>
    public class ImportEmailService : IProviderEmailService
    {
        public Task SyncMailAccountAsync(MailAccount account, string? jobId = null)
        {
            throw new NotSupportedException("Import provider does not support sync operations");
        }

        public Task<bool> TestConnectionAsync(MailAccount account)
        {
            throw new NotSupportedException("Import provider does not support connection testing");
        }

        public Task<List<string>> GetMailFoldersAsync(int accountId)
        {
            throw new NotSupportedException("Import provider does not have folders");
        }

        public Task<bool> RestoreEmailToFolderAsync(int emailId, int targetAccountId, string folderName)
        {
            throw new NotSupportedException("Import provider does not support email restoration");
        }

        public Task<(int Successful, int Failed)> RestoreMultipleEmailsWithProgressAsync(
            List<int> emailIds,
            int targetAccountId,
            string folderName,
            Action<int, int, int> progressCallback,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Import provider does not support email restoration");
        }

        public Task<bool> ResyncAccountAsync(int accountId)
        {
            throw new NotSupportedException("Import provider does not support resync operations");
        }

        public Task<int> GetEmailCountByAccountAsync(int accountId)
        {
            throw new NotSupportedException("Import provider does not support this operation");
        }
    }
}

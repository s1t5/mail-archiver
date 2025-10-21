using MailArchiver.Models;
using MailArchiver.Models.ViewModels;
using MailArchiver.ViewModels;

namespace MailArchiver.Services
{
    public interface IEmailService
    {
        Task SyncMailAccountAsync(MailAccount account, string? jobId = null);
        Task<(List<ArchivedEmail> Emails, int TotalCount)> SearchEmailsAsync(
            string searchTerm,
            DateTime? fromDate,
            DateTime? toDate,
            int? accountId,
            string folderName,
            bool? isOutgoing,
            int skip,
            int take,
            List<int> allowedAccountIds = null,
            string sortBy = "SentDate",
            string sortOrder = "desc");
        Task<byte[]> ExportEmailsAsync(ExportViewModel parameters, List<int> allowedAccountIds = null);
        Task<DashboardViewModel> GetDashboardStatisticsAsync();
        Task<bool> TestConnectionAsync(MailAccount account);
        Task<bool> RestoreEmailToFolderAsync(int emailId, int targetAccountId, string folderName);
        Task<List<string>> GetMailFoldersAsync(int accountId);
        Task<(int Successful, int Failed)> RestoreMultipleEmailsWithProgressAsync(List<int> emailIds, int targetAccountId, string folderName, Action<int, int, int> progressCallback, CancellationToken cancellationToken = default);
        Task<bool> ResyncAccountAsync(int accountId);
        Task<int> GetEmailCountByAccountAsync(int accountId);
    }
}

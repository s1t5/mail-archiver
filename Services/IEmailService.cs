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
            bool? isOutgoing,
            int skip,
            int take,
            List<int> allowedAccountIds = null);
        Task<byte[]> ExportEmailsAsync(ExportViewModel parameters, List<int> allowedAccountIds = null);
        Task<DashboardViewModel> GetDashboardStatisticsAsync();
        Task<bool> TestConnectionAsync(MailAccount account);
        Task<bool> RestoreEmailToFolderAsync(int emailId, int targetAccountId, string folderName);
        Task<List<string>> GetMailFoldersAsync(int accountId);
        Task<(int Successful, int Failed)> RestoreMultipleEmailsAsync(List<int> emailIds, int targetAccountId, string folderName);
        Task<bool> ResyncAccountAsync(int accountId);
        Task<int> GetEmailCountByAccountAsync(int accountId);
    }
}

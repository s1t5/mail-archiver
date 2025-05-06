using MailArchiver.Models;
using MailArchiver.Models.ViewModels;

namespace MailArchiver.Services
{
    public interface IEmailService
    {
        Task SyncMailAccountAsync(MailAccount account);
        Task<(List<ArchivedEmail> Emails, int TotalCount)> SearchEmailsAsync(
            string searchTerm, 
            DateTime? fromDate, 
            DateTime? toDate, 
            int? accountId, 
            bool? isOutgoing, 
            int skip, 
            int take);
        Task<byte[]> ExportEmailsAsync(ExportViewModel parameters);
        Task<DashboardViewModel> GetDashboardStatisticsAsync();
        Task<bool> TestConnectionAsync(MailAccount account);
    }
}
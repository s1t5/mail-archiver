using MailArchiver.Models;

namespace MailArchiver.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalEmails { get; set; }
        public int TotalAccounts { get; set; }
        public int TotalAttachments { get; set; }
        public string TotalStorageUsed { get; set; } // Formatiert als MB/GB
        public List<AccountStatistics> EmailsPerAccount { get; set; }
        public List<EmailCountByPeriod> EmailsByMonth { get; set; }
        public List<EmailCountByAddress> TopSenders { get; set; }
        public List<RecentEmailDto> RecentEmails { get; set; }
    }

    /// <summary>
    /// Slim projection of an archived email for the dashboard's recent-emails list.
    /// Deliberately excludes body/raw-header/bytea columns so the dashboard
    /// does not transfer megabytes of unused data per render.
    /// </summary>
    public class RecentEmailDto
    {
        public int Id { get; set; }
        public string Subject { get; set; }
        public string From { get; set; }
        public DateTime SentDate { get; set; }
        public bool IsOutgoing { get; set; }
        public string MailAccountName { get; set; }
    }

    public class AccountStatistics
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; }
        public string EmailAddress { get; set; }
        public int EmailCount { get; set; }
        public DateTime LastSyncTime { get; set; }
        public bool IsEnabled { get; set; }
        public ProviderType Provider { get; set; }
        public bool IsSyncing { get; set; }
        public bool IsSyncPending { get; set; }
        public string StorageUsed { get; set; }
    }

    public class EmailCountByPeriod
    {
        public string Period { get; set; }
        public int Count { get; set; }
    }

    public class EmailCountByAddress
    {
        public string EmailAddress { get; set; }
        public int Count { get; set; }
    }
}
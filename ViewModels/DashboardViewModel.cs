namespace MailArchiver.Models.ViewModels
{
    public readonly record struct StorageSizes(long DatabaseSize, long AttachmentsSize);

    public class DashboardViewModel
    {
        public int TotalEmails { get; set; }
        public int TotalAccounts { get; set; }
        public int TotalAttachments { get; set; }
        public string TotalStorageUsed { get; set; }
        public string TotalAttachmentsStorageUsed { get; set; }
        public List<AccountStatistics> EmailsPerAccount { get; set; }
        public List<EmailCountByPeriod> EmailsByMonth { get; set; }
        public List<EmailCountByAddress> TopSenders { get; set; }
        public List<ArchivedEmail> RecentEmails { get; set; }
    }

    public class AccountStatistics
    {
        public string AccountName { get; set; }
        public string EmailAddress { get; set; }
        public int EmailCount { get; set; }
        public DateTime LastSyncTime { get; set; }
        public bool IsEnabled { get; set; }
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
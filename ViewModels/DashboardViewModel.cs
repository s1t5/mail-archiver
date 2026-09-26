using MailArchiver.Models;

namespace MailArchiver.Models.ViewModels
{
    public class DashboardViewModel
    {
        public int TotalEmails { get; set; }
        public int IncomingEmails { get; set; }
        public int OutgoingEmails { get; set; }
        public int TotalAccounts { get; set; }

        /// <summary>
        /// Distinct mail domains among the accounts counted in <see cref="TotalAccounts"/>.
        /// </summary>
        public int AccountDomains { get; set; }

        public int TotalAttachments { get; set; }
        public int IncomingAttachments { get; set; }
        public int OutgoingAttachments { get; set; }
        public string TotalStorageUsed { get; set; } // Formatiert als MB/GB
        public List<AccountStatistics> EmailsPerAccount { get; set; }

        /// <summary>
        /// The two charts for the period the dashboard starts on. Same type the chart endpoint
        /// returns for every later selection, so the first paint and every redraw are built from
        /// one definition of a series.
        /// </summary>
        public DashboardSeries Series { get; set; }

        public List<RecentEmailDto> RecentEmails { get; set; }

        /// <summary>
        /// Whether the counter cards carry their incoming and outgoing parts. Set per request
        /// from the configuration rather than cached with the statistics, so it can never be a
        /// stale copy of the setting.
        /// </summary>
        public bool ShowDirectionSplits { get; set; }

        /// <summary>
        /// Whether the charts offer a resolution, a period and arrows to move it.
        /// </summary>
        public bool SelectablePeriods { get; set; }
    }

    /// <summary>
    /// The dashboard charts for one chosen period: mail per bucket split by direction, and the
    /// senders of that period in the chosen direction.
    /// </summary>
    public class DashboardSeries
    {
        /// <summary>Bucket width actually used, which may differ from what was asked for.</summary>
        public string Granularity { get; set; }

        /// <summary>Window actually used, which may differ from what was asked for.</summary>
        public string Window { get; set; }

        /// <summary>Whether <see cref="TopSenders"/> counts sent mail instead of received.</summary>
        public bool OutgoingSenders { get; set; }

        /// <summary>
        /// Whole windows this series sits before the one holding the current instant, zero being
        /// that window. Actually used, which may differ from what was asked for.
        /// </summary>
        public int Offset { get; set; }

        /// <summary>Whether a window further back would still meet mail.</summary>
        public bool CanGoBack { get; set; }

        /// <summary>Whether this is not the window holding the current instant.</summary>
        public bool CanGoForward { get; set; }

        /// <summary>
        /// The period as one line, for a card that can be paged: the two pickers no longer say
        /// where the reader is once the window can move.
        /// </summary>
        public string RangeLabel { get; set; }

        public List<EmailCountByPeriod> Emails { get; set; } = new();
        public List<EmailCountByAddress> TopSenders { get; set; } = new();
    }

    /// <summary>
    /// A count split by direction. The total is derived from the two parts rather than
    /// counted on its own, so the three numbers shown on one counter card always add up.
    /// </summary>
    public class DirectionCounts
    {
        public int Incoming { get; set; }
        public int Outgoing { get; set; }
        public int Total => Incoming + Outgoing;
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
        public bool LastRunHadIssues { get; set; }
        public string StorageUsed { get; set; }
    }

    /// <summary>
    /// One bucket of a chart. The two directions are stacked, so the bucket carries both and
    /// derives its total from them for the same reason the counter cards do.
    /// </summary>
    public class EmailCountByPeriod
    {
        public string Period { get; set; }
        public int Incoming { get; set; }
        public int Outgoing { get; set; }
        public int Count => Incoming + Outgoing;
    }

    public class EmailCountByAddress
    {
        public string EmailAddress { get; set; }
        public int Count { get; set; }
    }
}
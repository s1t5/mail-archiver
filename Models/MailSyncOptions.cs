namespace MailArchiver.Models
{
    public class MailSyncOptions
    {
        public const string MailSync = "MailSync";
        
        public int IntervalMinutes { get; set; } = 5;
        public int? FullSyncIntervalHours { get; set; }
        public int TimeoutMinutes { get; set; } = 60;
        public int ConnectionTimeoutSeconds { get; set; } = 180;
        public int CommandTimeoutSeconds { get; set; } = 300;
        public bool AlwaysForceFullSync { get; set; } = false;
        public bool IgnoreSelfSignedCert { get; set; } = false;
        public int MaxConcurrentSyncs { get; set; } = 1;
        public int InterAccountDelaySeconds { get; set; } = 0;

        /// <summary>
        /// Folders excluded from synchronization for every account of this installation, on top of
        /// each account's own exclusion list. Additive: a folder is skipped when it matches either
        /// list, and the same matching rules apply to both.
        ///
        /// Empty by default, so existing installations are unaffected. Intended for importing many
        /// mailboxes from the same server, where maintaining an identical exclusion list on every
        /// account is the only alternative. No folder name is ever excluded by default — the names
        /// worth listing depend entirely on the server and its language.
        /// </summary>
        public List<string> GlobalExcludedFolders { get; set; } = new();
    }
}

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
    }
}

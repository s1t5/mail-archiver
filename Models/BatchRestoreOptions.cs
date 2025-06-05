// Models/BatchRestoreOptions.cs
namespace MailArchiver.Models
{
    public class BatchRestoreOptions
    {
        public const string BatchRestore = "BatchRestore";
        public int AsyncThreshold { get; set; } = 500;

        public int MaxSyncEmails { get; set; } = 2000;

        public int MaxAsyncEmails { get; set; } = 50000;

        public int SessionTimeoutMinutes { get; set; } = 30;

        public int DefaultBatchSize { get; set; } = 50;
    }
}
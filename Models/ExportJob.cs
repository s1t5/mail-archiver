namespace MailArchiver.Models
{
    public class AccountExportJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public int MailAccountId { get; set; }
        public string MailAccountName { get; set; } = string.Empty;
        public AccountExportFormat Format { get; set; }
        public string UserId { get; set; } = "System";
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime? Started { get; set; }
        public DateTime? Completed { get; set; }
        public AccountExportJobStatus Status { get; set; } = AccountExportJobStatus.Queued;
        public int ProcessedEmails { get; set; }
        public int TotalEmails { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CurrentEmailSubject { get; set; }
        public string? OutputFilePath { get; set; }
        public long OutputFileSize { get; set; }
        public int IncomingEmailsCount { get; set; }
        public int OutgoingEmailsCount { get; set; }
    }

    public enum AccountExportJobStatus
    {
        Queued,
        Running,
        Completed,
        Failed,
        Cancelled,
        Downloaded
    }

    public enum AccountExportFormat
    {
        EML,
        MBox
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MailArchiver.Models
{
    public class SelectedEmailsExportJob
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public List<int> EmailIds { get; set; } = new List<int>();
        public string UserId { get; set; } = "System";
        public DateTime Created { get; set; } = DateTime.UtcNow;
        public DateTime? Started { get; set; }
        public DateTime? Completed { get; set; }
        public SelectedEmailsExportJobStatus Status { get; set; } = SelectedEmailsExportJobStatus.Queued;
        public int ProcessedEmails { get; set; }
        public int TotalEmails { get; set; }
        public string? ErrorMessage { get; set; }
        public string? CurrentEmailSubject { get; set; }
        public string? OutputFilePath { get; set; }
        public long OutputFileSize { get; set; }
        public AccountExportFormat Format { get; set; } = AccountExportFormat.EML;
    }

    public enum SelectedEmailsExportJobStatus
    {
        Queued,
        Running,
        Completed,
        Failed,
        Cancelled,
        Downloaded
    }
}

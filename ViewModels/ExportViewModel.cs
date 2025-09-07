using MailArchiver.Models;
using System.ComponentModel.DataAnnotations;

namespace MailArchiver.ViewModels
{
    // For individual email exports (used by EmailsController)
    public class ExportViewModel
    {
        public string? SearchTerm { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public int? SelectedAccountId { get; set; }
        public bool? IsOutgoing { get; set; }
        public int? EmailId { get; set; }
        public ExportFormat Format { get; set; } = ExportFormat.Eml;
    }

    // For account exports (used by MailAccountsController)
    public class AccountExportViewModel
    {
        public int MailAccountId { get; set; }
        public string MailAccountName { get; set; } = string.Empty;
        public int IncomingEmailsCount { get; set; }
        public int OutgoingEmailsCount { get; set; }
        public int TotalEmailsCount { get; set; }

        [Required]
        public AccountExportFormat Format { get; set; } = AccountExportFormat.EML;
    }

    // Export formats for individual emails
    public enum ExportFormat
    {
        Csv,
        Json,
        Eml
    }
}

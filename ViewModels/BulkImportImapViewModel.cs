using System.ComponentModel.DataAnnotations;
using MailArchiver;
using MailArchiver.Models;
using Microsoft.Extensions.Localization;

namespace MailArchiver.Models.ViewModels
{
    public class BulkImportImapViewModel : IValidatableObject
    {
        [Required]
        [Display(Name = "CSV file")]
        public IFormFile? CsvFile { get; set; }

        [Display(Name = "IMAP server")]
        public string? ImapServer { get; set; }

        [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
        [Display(Name = "IMAP port")]
        public int ImapPort { get; set; } = 993;

        [Display(Name = "Use SSL")]
        public bool UseSSL { get; set; } = true;

        [Display(Name = "Account Enabled")]
        public bool IsEnabled { get; set; } = true;

        [Display(Name = "Name prefix")]
        public string? NamePrefix { get; set; }

        [Display(Name = "Delete After Days")]
        [Range(1, int.MaxValue, ErrorMessage = "Delete after days must be at least 1")]
        public int? DeleteAfterDays { get; set; }

        [Display(Name = "Local Retention Days")]
        [Range(1, int.MaxValue, ErrorMessage = "Local retention days must be at least 1")]
        public int? LocalRetentionDays { get; set; }

        [Display(Name = "Skip existing mailboxes")]
        public bool SkipExisting { get; set; } = true;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var localizer = validationContext.GetService(typeof(IStringLocalizer<SharedResource>)) as IStringLocalizer<SharedResource>;

            if (LocalRetentionDays.HasValue && !DeleteAfterDays.HasValue)
            {
                yield return new ValidationResult(
                    localizer?["LocalRetentionRequiresServerRetention"].Value ?? "Local retention requires a server retention value.",
                    new[] { nameof(LocalRetentionDays) });
            }

            if (LocalRetentionDays.HasValue && DeleteAfterDays.HasValue &&
                LocalRetentionDays.Value < DeleteAfterDays.Value)
            {
                yield return new ValidationResult(
                    localizer?["LocalRetentionMustBeGreaterOrEqual"].Value ?? "Local retention must be greater than or equal to server retention.",
                    new[] { nameof(LocalRetentionDays) });
            }
        }
    }
}
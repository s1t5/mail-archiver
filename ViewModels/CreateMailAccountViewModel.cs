using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MailArchiver;
using MailArchiver.Attributes;
using MailArchiver.Models;
using Microsoft.Extensions.Localization;

namespace MailArchiver.Models.ViewModels
{
    public class CreateMailAccountViewModel : IValidatableObject
    {
        [Display(Name = "Account name")]
        public string? Name { get; set; }
        
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Email address")]
        public string? EmailAddress { get; set; }
        
        [ConditionalRequired(nameof(Provider), ProviderType.IMAP, ErrorMessage = "IMAP server is required for IMAP accounts")]
        [Display(Name = "IMAP server")]
        public string? ImapServer { get; set; }
        
        [ConditionalRequired(nameof(Provider), ProviderType.IMAP, ErrorMessage = "IMAP port is required for IMAP accounts")]
        [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
        [Display(Name = "IMAP port")]
        public int ImapPort { get; set; } = 993;
        
        [ConditionalRequired(nameof(Provider), ProviderType.IMAP, ErrorMessage = "Username is required for IMAP accounts")]
        [Display(Name = "Username")]
        public string? Username { get; set; }
        
        [ConditionalRequired(nameof(Provider), ProviderType.IMAP, ErrorMessage = "Password is required for IMAP accounts")]
        [Display(Name = "Password")]
        public string? Password { get; set; }
        
        [Display(Name = "Use SSL")]
        public bool UseSSL { get; set; } = true;
        
        [Display(Name = "Account Enabled")]
        public bool IsEnabled { get; set; } = true;
        
        [Display(Name = "Provider")]
        public ProviderType Provider { get; set; } = ProviderType.IMAP;
        
        [Display(Name = "Client ID")]
        [ConditionalRequired(nameof(Provider), ProviderType.M365, ErrorMessage = "Client ID is required for M365 accounts")]
        public string? ClientId { get; set; }
        
        [Display(Name = "Client Secret")]
        [ConditionalRequired(nameof(Provider), ProviderType.M365, ErrorMessage = "Client Secret is required for M365 accounts")]
        public string? ClientSecret { get; set; }
        
        [Display(Name = "Tenant ID")]
        [ConditionalRequired(nameof(Provider), ProviderType.M365, ErrorMessage = "Tenant ID is required for M365 accounts")]
        public string? TenantId { get; set; }

        [Display(Name = "Import from tenant")]
        public bool ImportEntireTenant { get; set; } = true;

        [Display(Name = "Import all listed mailboxes")]
        public bool ImportAllTenantMailboxes { get; set; } = true;

        [Display(Name = "Skip disabled mailboxes")]
        public bool SkipDisabledMailboxes { get; set; } = true;

        public List<string> SelectedM365Mailboxes { get; set; } = new();
        
        [Display(Name = "Delete After Days")]
        [Range(1, int.MaxValue, ErrorMessage = "Delete after days must be at least 1")]
        public int? DeleteAfterDays { get; set; }
        
        [Display(Name = "Local Retention Days")]
        [Range(1, int.MaxValue, ErrorMessage = "Local retention days must be at least 1")]
        public int? LocalRetentionDays { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var localizer = validationContext.GetService(typeof(IStringLocalizer<SharedResource>)) as IStringLocalizer<SharedResource>;

            if (string.IsNullOrWhiteSpace(Name))
            {
                yield return new ValidationResult(
                    localizer?["NameRequired"].Value ?? "Name is required",
                    new[] { nameof(Name) });
            }

            if (Provider != ProviderType.M365 || !ImportEntireTenant)
            {
                if (string.IsNullOrWhiteSpace(EmailAddress))
                {
                    yield return new ValidationResult(
                        localizer?["EmailAddressRequired"].Value ?? "Email address is required",
                        new[] { nameof(EmailAddress) });
                }
            }

            if (Provider == ProviderType.M365 && ImportEntireTenant && !ImportAllTenantMailboxes &&
                (SelectedM365Mailboxes == null || SelectedM365Mailboxes.Count == 0))
            {
                yield return new ValidationResult(
                    localizer?["SelectAtLeastOneMailboxOrImportAll"].Value ?? "Select at least one mailbox or enable importing all listed mailboxes.",
                    new[] { nameof(SelectedM365Mailboxes) });
            }
        }
    }
}

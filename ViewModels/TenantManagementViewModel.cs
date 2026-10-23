using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MailArchiver;
using MailArchiver.Models;
using Microsoft.Extensions.Localization;

namespace MailArchiver.Models.ViewModels
{
    public class TenantMailboxViewModel
    {
        public string DisplayName { get; set; } = string.Empty;
        public string EmailAddress { get; set; } = string.Empty;
        public bool IsDisabled { get; set; }
        public bool AlreadyExists { get; set; }
    }

    public class TenantManagementViewModel : IValidatableObject
    {
        public int SourceAccountId { get; set; }

        [Display(Name = "Source account")]
        public string SourceAccountName { get; set; } = string.Empty;

        [Display(Name = "Email address")]
        public string SourceEmailAddress { get; set; } = string.Empty;

        [Display(Name = "Account name")]
        public string? Name { get; set; }

        [Display(Name = "Rename existing accounts")]
        public bool RenameExistingAccounts { get; set; } = false;

        [Display(Name = "Delete After Days")]
        [Range(1, int.MaxValue, ErrorMessage = "Delete after days must be at least 1")]
        public int? DeleteAfterDays { get; set; }

        [Display(Name = "Local Retention Days")]
        [Range(1, int.MaxValue, ErrorMessage = "Local retention days must be at least 1")]
        public int? LocalRetentionDays { get; set; }

        public List<TenantMailboxViewModel> Mailboxes { get; set; } = new();

        public List<string> SelectedMailboxes { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var localizer = validationContext.GetService(typeof(IStringLocalizer<SharedResource>)) as IStringLocalizer<SharedResource>;

            if (string.IsNullOrWhiteSpace(Name))
            {
                yield return new ValidationResult(
                    localizer?["NameRequired"].Value ?? "Name is required",
                    new[] { nameof(Name) });
            }
            else if (Name.Trim().Length > 200)
            {
                yield return new ValidationResult(
                    localizer?["AccountNameTooLong"].Value ?? "Account name must be at most 200 characters.",
                    new[] { nameof(Name) });
            }

            if (!RenameExistingAccounts && (SelectedMailboxes == null || SelectedMailboxes.Count == 0))
            {
                yield return new ValidationResult(
                    localizer?["SelectAtLeastOneMailboxForManagement"].Value ?? "Select at least one mailbox to add.",
                    new[] { nameof(SelectedMailboxes) });
            }

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
                    localizer?["LocalRetentionMustBeGreaterOrEqual"].Value ?? "Local retention days must be greater than or equal to delete after days.",
                    new[] { nameof(LocalRetentionDays) });
            }
        }
    }
}

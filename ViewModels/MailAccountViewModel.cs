using System.ComponentModel.DataAnnotations;
using MailArchiver.Attributes;
using MailArchiver.Models;

namespace MailArchiver.Models.ViewModels
{
    public class MailAccountViewModel
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Name is required")]
        [Display(Name = "Account name")]
        public string Name { get; set; }
        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Email address")]
        public string EmailAddress { get; set; }
        [Display(Name = "IMAP server")]
        [ConditionalRequired(nameof(Provider), ProviderType.IMAP, ErrorMessage = "IMAP server is required for IMAP accounts")]
        public string? ImapServer { get; set; }
        [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
        [Display(Name = "IMAP port")]
        [ConditionalRequired(nameof(Provider), ProviderType.IMAP, ErrorMessage = "IMAP port is required for IMAP accounts")]
        public int? ImapPort { get; set; } = 993;
        [Display(Name = "Username")]
        [ConditionalRequired(nameof(Provider), ProviderType.IMAP, ErrorMessage = "Username is required for IMAP accounts")]
        public string? Username { get; set; }
        
        [Display(Name = "Password")]
        public string? Password { get; set; }
        
        [Display(Name = "Use SSL")]
        public bool UseSSL { get; set; } = true;
        
        [Display(Name = "Last sync")]
        public DateTime? LastSync { get; set; }

        [Display(Name = "Account Enabled")]
        public bool IsEnabled { get; set; } = true;

        [Display(Name = "Excluded Folders")]
        public string? ExcludedFolders { get; set; } = string.Empty;
        
        [Display(Name = "Delete After Days")]
        [Range(1, int.MaxValue, ErrorMessage = "Delete after days must be at least 1")]
        public int? DeleteAfterDays { get; set; }
        
        [Display(Name = "Provider")]
        public ProviderType Provider { get; set; } = ProviderType.IMAP;
        
        [Display(Name = "Client ID")]
        [ConditionalRequired(nameof(Provider), ProviderType.M365, ErrorMessage = "Client ID is required for M365 accounts")]
        public string? ClientId { get; set; }
        
        [Display(Name = "Client Secret")]
        public string? ClientSecret { get; set; }
        
        [Display(Name = "Tenant ID")]
        [ConditionalRequired(nameof(Provider), ProviderType.M365, ErrorMessage = "Tenant ID is required for M365 accounts")]
        public string? TenantId { get; set; }
        
        // For UI display of available folders
        public List<string> AvailableFolders { get; set; } = new List<string>();

        // Flag to determine if it's a new or existing account
        public bool IsNewAccount => Id == 0;
    }
}

using System.ComponentModel.DataAnnotations;
using MailArchiver.Attributes;

namespace MailArchiver.Models.ViewModels
{
    public class CreateMailAccountViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        [Display(Name = "Account name")]
        public string Name { get; set; }
        
        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [Display(Name = "Email address")]
        public string EmailAddress { get; set; }
        
        [ConditionalRequired(nameof(IsImportOnly), false, ErrorMessage = "IMAP server is required")]
        [Display(Name = "IMAP server")]
        public string ImapServer { get; set; }
        
        [ConditionalRequired(nameof(IsImportOnly), false, ErrorMessage = "IMAP port is required")]
        [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
        [Display(Name = "IMAP port")]
        public int ImapPort { get; set; } = 993;
        
        [ConditionalRequired(nameof(IsImportOnly), false, ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        public string Username { get; set; }
        
        [ConditionalRequired(nameof(IsImportOnly), false, ErrorMessage = "Password is required")]
        [Display(Name = "Password")]
        public string Password { get; set; }
        
        [Display(Name = "Use SSL")]
        public bool UseSSL { get; set; } = true;
        
        [Display(Name = "Account Enabled")]
        public bool IsEnabled { get; set; } = true;
        
        [Display(Name = "Import Only Account")]
        public bool IsImportOnly { get; set; } = false;
        
        [Display(Name = "Delete After Days")]
        [Range(1, int.MaxValue, ErrorMessage = "Delete after days must be at least 1")]
        public int? DeleteAfterDays { get; set; }
    }
}

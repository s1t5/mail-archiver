using System.ComponentModel.DataAnnotations;

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
        
        [Required(ErrorMessage = "IMAP server is required")]
        [Display(Name = "IMAP server")]
        public string ImapServer { get; set; }
        
        [Required(ErrorMessage = "IMAP port is required")]
        [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
        [Display(Name = "IMAP port")]
        public int ImapPort { get; set; } = 993;
        
        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        public string Username { get; set; }
        
        [Required(ErrorMessage = "Password is required")]
        [Display(Name = "Password")]
        public string Password { get; set; }
        
        [Display(Name = "Use SSL")]
        public bool UseSSL { get; set; } = true;
        
        [Display(Name = "Account Enabled")]
        public bool IsEnabled { get; set; } = true;
    }
}

// Models/ViewModels/EmailRestoreViewModel.cs
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models.ViewModels
{
    public class EmailRestoreViewModel
    {
        public int EmailId { get; set; }
        
        [Required(ErrorMessage = "Please select a target account")]
        [Display(Name = "Target Account")]
        public int TargetAccountId { get; set; }
        
        [Required(ErrorMessage = "Please select a target folder")]
        [Display(Name = "Target Folder")]
        public string TargetFolder { get; set; } = "INBOX";
        
        // For dropdown selection
        public List<SelectListItem> AvailableAccounts { get; set; } = new List<SelectListItem>();
        
        // For folder dropdown
        public List<SelectListItem> AvailableFolders { get; set; } = new List<SelectListItem>();
        
        public string EmailSubject { get; set; }
        public DateTime EmailDate { get; set; }
        public string EmailSender { get; set; }
    }
}
// Models/ViewModels/BatchRestoreViewModel.cs
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models.ViewModels
{
    public class BatchRestoreViewModel
    {
        [Display(Name = "Selected Emails")]
        public List<int> SelectedEmailIds { get; set; } = new List<int>();
        
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
        
        // Summary information for display
        public int EmailCount => SelectedEmailIds?.Count ?? 0;
        
        // Whether to redirect to search results or specific detail page
        public string ReturnUrl { get; set; }
    }
}
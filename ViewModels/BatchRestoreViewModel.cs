// ViewModels/BatchRestoreViewModel.cs
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models.ViewModels
{
    public class BatchRestoreViewModel
    {
        public List<int> SelectedEmailIds { get; set; } = new();

        [Required(ErrorMessage = "Please select a target account")]
        [Display(Name = "Target Account")]
        public int TargetAccountId { get; set; }

        [Required(ErrorMessage = "Please enter a target folder")]
        [Display(Name = "Target Folder")]
        public string TargetFolder { get; set; } = "INBOX";

        [Display(Name = "Preserve Folder Structure")]
        public bool PreserveFolderStructure { get; set; } = false;

        public List<SelectListItem> AvailableAccounts { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> AvailableFolders { get; set; } = new List<SelectListItem>();

        public int EmailCount => SelectedEmailIds?.Count ?? 0;
        public string ReturnUrl { get; set; } = "";
    }
}
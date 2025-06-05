// ViewModels/AsyncBatchRestoreViewModel.cs
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models.ViewModels
{
    public class AsyncBatchRestoreViewModel
    {
        public List<int> EmailIds { get; set; } = new List<int>();

        [Required(ErrorMessage = "Please select a target account")]
        [Display(Name = "Target Account")]
        public int TargetAccountId { get; set; }

        [Required(ErrorMessage = "Please select a target folder")]
        [Display(Name = "Target Folder")]
        public string TargetFolder { get; set; } = "INBOX";

        public List<SelectListItem> AvailableAccounts { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> AvailableFolders { get; set; } = new List<SelectListItem>();

        public int EmailCount => EmailIds?.Count ?? 0;
        public string ReturnUrl { get; set; } = "";
    }
}
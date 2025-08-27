using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models.ViewModels
{
    public class MBoxImportViewModel
    {
        [Required(ErrorMessage = "Please select an MBox file")]
        [Display(Name = "MBox File")]
        public IFormFile MBoxFile { get; set; }

        [Required(ErrorMessage = "Please select a target account")]
        [Display(Name = "Target Account")]
        public int TargetAccountId { get; set; }

        [Required(ErrorMessage = "Please select a target folder")]
        [Display(Name = "Target Folder")]
        public string TargetFolder { get; set; } = "INBOX";

        public List<SelectListItem> AvailableAccounts { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> AvailableFolders { get; set; } = new List<SelectListItem>();

        public long MaxFileSize { get; set; }
        public string MaxFileSizeFormatted => FormatFileSize(MaxFileSize);

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}

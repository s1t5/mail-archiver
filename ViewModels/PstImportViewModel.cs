using Microsoft.AspNetCore.Mvc.Rendering;

using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models.ViewModels
{
    public class PstImportViewModel
    {
        [Required(ErrorMessage = "Please select a PST file")]
        [Display(Name = "PST File")]
        public IFormFile PstFile { get; set; }

        [Required(ErrorMessage = "Please select a target account")]
        [Display(Name = "Target Account")]
        public int TargetAccountId { get; set; }

        [Display(Name = "Fallback Folder")]
        public string TargetFolder { get; set; } = "INBOX";

        [Display(Name = "Preserve Folder Structure")]
        public bool PreserveFolderStructure { get; set; } = true;

        public List<SelectListItem> AvailableAccounts { get; set; } = new();
        public Dictionary<int, ProviderType> AccountProviders { get; set; } = new();

        public long MaxFileSize { get; set; }
        public string MaxFileSizeFormatted => FormatFileSize(MaxFileSize);

        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
            var counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1) { number /= 1024; counter++; }

            return $"{number:n1} {suffixes[counter]}";
        }
    }
}

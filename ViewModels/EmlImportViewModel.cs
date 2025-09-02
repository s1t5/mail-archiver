using Microsoft.AspNetCore.Mvc.Rendering;

namespace MailArchiver.Models.ViewModels
{
    public class EmlImportViewModel
    {
        public IFormFile? EmlFile { get; set; }
        public int TargetAccountId { get; set; }
        public List<SelectListItem> AvailableAccounts { get; set; } = new();
        public long MaxFileSize { get; set; }
        
        public string MaxFileSizeFormatted => 
            MaxFileSize >= 1073741824 ? $"{MaxFileSize / 1073741824.0:F1} GB" :
            MaxFileSize >= 1048576 ? $"{MaxFileSize / 1048576.0:F1} MB" :
            MaxFileSize >= 1024 ? $"{MaxFileSize / 1024.0:F1} KB" :
            $"{MaxFileSize} bytes";
    }
}

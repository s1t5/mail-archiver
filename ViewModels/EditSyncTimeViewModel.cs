using System.ComponentModel.DataAnnotations;

namespace MailArchiver.ViewModels
{
    public class EditSyncTimeViewModel
    {
        public int Id { get; set; }
        public string AccountName { get; set; }
        public DateTime CurrentSyncTime { get; set; }
        
        [Required(ErrorMessage = "New sync time is required")]
        [Display(Name = "New Sync Time")]
        public DateTime NewSyncTime { get; set; } = DateTime.UtcNow;
    }
}

using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models.ViewModels
{
    public class CreateApiKeyViewModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public DateTime? ExpiresAt { get; set; }
    }
}

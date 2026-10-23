using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models.ViewModels
{
    public class EditUserViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; }

        [Required]
        [StringLength(100)]
        [EmailAddress]
        public string Email { get; set; }

        public bool IsAdmin { get; set; }

        public bool IsSelfManager { get; set; }

        public bool IsActive { get; set; }
    }
}

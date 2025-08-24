using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models
{
    public class UserSession
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string Token { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Username { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime? ExpiresAt { get; set; }
    }
}

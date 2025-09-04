using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models
{
    public class User
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(50)]
        public string Username { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Email { get; set; }
        
        [Required]
        public string PasswordHash { get; set; }
        
        [Required]
        public bool IsAdmin { get; set; } = false;
        
        [Required]
        public bool IsSelfManager { get; set; } = false;
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastLoginAt { get; set; }
        
        // 2FA TOTP properties
        public bool IsTwoFactorEnabled { get; set; } = false;
        public string? TwoFactorSecret { get; set; }
        public string? TwoFactorBackupCodes { get; set; }
        
        // Navigation properties
        public virtual ICollection<UserMailAccount> UserMailAccounts { get; set; } = new List<UserMailAccount>();
    }
}

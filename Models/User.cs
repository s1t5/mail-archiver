using System.ComponentModel.DataAnnotations;

namespace MailArchiver.Models
{
    public class User
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(320)]
        public string Username { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Email { get; set; }
        
        public string? PasswordHash { get; set; }

        /// <summary>
        /// Rotated whenever the account's security state changes (password change or
        /// reset, activation status, role change). Every auth cookie carries the value
        /// it was issued with; a mismatch ends the session on the next request.
        /// </summary>
        [Required]
        [StringLength(32)]
        public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
        
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

        // OAuth properties
        public string? OAuthRemoteUserId { get; set; }
        public bool RequiresApproval { get; set; } = false;

        // Version update tracking
        public string? LastSeenChangelogVersion { get; set; }
        
        // Navigation properties
        public virtual ICollection<UserMailAccount> UserMailAccounts { get; set; } = new List<UserMailAccount>();
        public virtual ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    }
}

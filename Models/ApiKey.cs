using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MailArchiver.Models
{
    /// <summary>
    /// A per-user API key for the read-only REST API. The plaintext key is shown
    /// to the user exactly once at creation; only a non-reversible SHA-256 hash
    /// (and a short plaintext prefix for identification) is persisted.
    /// </summary>
    public class ApiKey
    {
        public int Id { get; set; }

        /// <summary>Owning user. A key inherits this user's mailbox permissions.</summary>
        [Required]
        public int UserId { get; set; }

        /// <summary>User-supplied descriptive label, e.g. "reporting script".</summary>
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// First characters of the key (e.g. "ma_8Kf3pQ2"), stored in plaintext for
        /// identification in the UI and logs. Not a secret.
        /// </summary>
        [Required]
        [StringLength(16)]
        public string KeyPrefix { get; set; } = string.Empty;

        /// <summary>Hex-encoded SHA-256 of the full key. Unique; used for O(1) lookup.</summary>
        [Required]
        [StringLength(64)]
        public string KeyHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Last time the key authenticated a request (throttled writes).</summary>
        public DateTime? LastUsedAt { get; set; }

        /// <summary>Optional expiry. A key past this instant is rejected.</summary>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Set when the key is revoked. A revoked key is permanently rejected.</summary>
        public DateTime? RevokedAt { get; set; }

        public virtual User User { get; set; } = null!;

        /// <summary>
        /// True when the key is usable: not revoked and not past its expiry. The
        /// owning user's own active state is checked separately during validation.
        /// </summary>
        [NotMapped]
        public bool IsActive => RevokedAt == null && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
    }
}

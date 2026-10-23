using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MailArchiver.Models
{
    /// <summary>
    /// Tracks daily bandwidth usage per mail account for rate limit management.
    /// Used to prevent IMAP rate limits from providers like Gmail (2500MB/day).
    /// </summary>
    [Table("BandwidthUsage", Schema = "mail_archiver")]
    public class BandwidthUsage
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// The mail account this usage record belongs to.
        /// </summary>
        [Required]
        public int MailAccountId { get; set; }

        [ForeignKey(nameof(MailAccountId))]
        public MailAccount? MailAccount { get; set; }

        /// <summary>
        /// The date this usage record is for (UTC date only, no time component).
        /// </summary>
        [Required]
        public DateTime Date { get; set; }

        /// <summary>
        /// Total bytes downloaded on this date.
        /// </summary>
        [Required]
        public long BytesDownloaded { get; set; } = 0;

        /// <summary>
        /// Total bytes uploaded on this date (if tracking is enabled).
        /// </summary>
        [Required]
        public long BytesUploaded { get; set; } = 0;

        /// <summary>
        /// Number of emails processed on this date.
        /// </summary>
        [Required]
        public int EmailsProcessed { get; set; } = 0;

        /// <summary>
        /// Whether the rate limit has been reached on this date.
        /// When true, sync should be paused until LimitResetTime.
        /// </summary>
        [Required]
        public bool LimitReached { get; set; } = false;

        /// <summary>
        /// When the rate limit will reset (if LimitReached is true).
        /// Null if no limit has been reached.
        /// </summary>
        public DateTime? LimitResetTime { get; set; }

        /// <summary>
        /// Timestamp when this record was created.
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when this record was last updated.
        /// </summary>
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
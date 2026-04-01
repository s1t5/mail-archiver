using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MailArchiver.Models
{
    /// <summary>
    /// Tracks sync progress per folder for resumable syncing.
    /// Used when sync is interrupted (e.g., by rate limits) to resume from the last position.
    /// Checkpoints are temporary and are deleted after successful sync completion.
    /// </summary>
    [Table("SyncCheckpoints", Schema = "mail_archiver")]
    public class SyncCheckpoint
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// The mail account this checkpoint belongs to.
        /// </summary>
        [Required]
        public int MailAccountId { get; set; }

        [ForeignKey(nameof(MailAccountId))]
        public MailAccount? MailAccount { get; set; }

        /// <summary>
        /// The folder name this checkpoint is for (e.g., "INBOX", "Sent", "Archive").
        /// </summary>
        [Required]
        [Column(TypeName = "text")]
        public string FolderName { get; set; } = string.Empty;

        /// <summary>
        /// The date of the last successfully synced message in this folder.
        /// Used to resume sync from this point.
        /// </summary>
        public DateTime? LastMessageDate { get; set; }

        /// <summary>
        /// The Message-ID of the last successfully synced message.
        /// Used for duplicate detection during resume.
        /// </summary>
        [Column(TypeName = "text")]
        public string? LastMessageId { get; set; }

        /// <summary>
        /// Number of messages processed in this folder so far.
        /// </summary>
        [Required]
        public int ProcessedCount { get; set; } = 0;

        /// <summary>
        /// Timestamp when this checkpoint was created.
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when this checkpoint was last updated.
        /// </summary>
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether this folder has been fully synced.
        /// When true, this checkpoint can be deleted after successful sync.
        /// </summary>
        [Required]
        public bool IsCompleted { get; set; } = false;

        /// <summary>
        /// Total bytes downloaded for this folder so far (for bandwidth tracking).
        /// </summary>
        public long BytesDownloaded { get; set; } = 0;
    }
}
namespace MailArchiver.Models
{
    /// <summary>
    /// Content-addressed storage for attachment payloads.
    /// Identical attachment bytes (same SHA-256 hash) are stored exactly once
    /// and referenced by many <see cref="EmailAttachment"/> rows (deduplication).
    /// </summary>
    public class AttachmentContent
    {
        public int Id { get; set; }

        /// <summary>SHA-256 hash of <see cref="Content"/> as lowercase hex string (64 chars).</summary>
        public string Hash { get; set; } = string.Empty;

        /// <summary>The raw attachment bytes (stored once per unique hash).</summary>
        public byte[] Content { get; set; } = Array.Empty<byte>();

        /// <summary>Size of <see cref="Content"/> in bytes.</summary>
        public long Size { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


        public virtual ICollection<EmailAttachment> Attachments { get; set; } = new List<EmailAttachment>();
    }
}

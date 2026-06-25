using MailArchiver.Services.Storage;

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

        /// <summary>SHA-256 hash of the payload as lowercase hex string (64 chars).</summary>
        public string Hash { get; set; } = string.Empty;

        /// <summary>
        /// Raw attachment bytes. Non-null when <see cref="StorageType"/> is
        /// <see cref="AttachmentStorageType.Database"/>; null when the payload has
        /// been moved to another storage.
        /// </summary>
        public byte[]? Content { get; set; }

        /// <summary>Size of the payload in bytes.</summary>
        public long Size { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Where the attachment bytes currently live.</summary>
        public AttachmentStorageType StorageType { get; set; } = AttachmentStorageType.Database;

        public virtual ICollection<EmailAttachment> Attachments { get; set; } = new List<EmailAttachment>();

        private byte[]? _cachedContent;

        public async Task<byte[]> GetContentAsync(AttachmentStorageFactory storageFactory,
            CancellationToken ct = default)
        {
            _cachedContent ??= await storageFactory.GetStorageFor(this).ReadAsync(this, ct);
            return _cachedContent;
        }
    }
}

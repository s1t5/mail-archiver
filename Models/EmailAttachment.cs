using MailArchiver.Services.Storage;
using System.ComponentModel.DataAnnotations.Schema;

namespace MailArchiver.Models
{
    public class EmailAttachment
    {
        public int Id { get; set; }
        public int ArchivedEmailId { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public string? ContentId { get; set; }
        public long Size { get; set; }

        // ============================================================
        // Attachment deduplication
        // ============================================================
        // The actual payload bytes are stored content-addressed (by SHA-256)
        // in the AttachmentContents table and shared between identical attachments.
        public int? AttachmentContentId { get; set; }
        public virtual AttachmentContent? AttachmentContent { get; set; }

        // Legacy inline storage. Maps to the original "Content" column on the
        // EmailAttachments table. Kept (nullable) during the transition so that
        // not-yet-migrated rows can still be read. The background migration moves
        // these bytes into AttachmentContents and sets this column to NULL.
        public byte[]? LegacyContent { get; set; }

        // Holds the bytes of a freshly created attachment until the
        // AttachmentDeduplicationInterceptor turns them into a deduplicated
        // AttachmentContent reference on SaveChanges. Not mapped to the database.
        private byte[]? _pendingContent;

        /// <summary>
        /// Convenience accessor used throughout the codebase.
        /// On read it resolves the bytes from the deduplicated content (preferred),
        /// then the legacy column, then any pending (not yet persisted) bytes.
        /// On write it stashes the bytes for the dedup interceptor.
        /// </summary>
        [NotMapped]
        public byte[] Content
        {
            get => AttachmentContent?.Content ?? LegacyContent ?? _pendingContent ?? Array.Empty<byte>();
            set => _pendingContent = value;
        }

        /// <summary>Pending bytes consumed by the dedup interceptor (internal use).</summary>
        [NotMapped]
        internal byte[]? PendingContent => _pendingContent;

        /// <summary>Clears the pending bytes after they have been deduplicated.</summary>
        internal void ClearPendingContent() => _pendingContent = null;

        public Task<byte[]> GetContentAsync(AttachmentStorageFactory storageFactory,
            CancellationToken ct = default)
        {
            if (AttachmentContent == null)
                throw new InvalidOperationException(
                    $"Attachment {Id} ({FileName}) has no AttachmentContent loaded. " +
                    "Ensure AttachmentContent is eagerly included in the query.");
            return AttachmentContent.GetContentAsync(storageFactory, ct);
        }

        public virtual ArchivedEmail ArchivedEmail { get; set; }
    }
}

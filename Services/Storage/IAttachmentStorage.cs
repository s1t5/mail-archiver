using MailArchiver.Models;

namespace MailArchiver.Services.Storage;

public interface IAttachmentStorage
{
    AttachmentStorageType StorageType { get; }

    Task<byte[]> ReadAsync(AttachmentContent content, CancellationToken ct = default);

    Task WriteAsync(AttachmentContent content, byte[] data, CancellationToken ct = default);

    Task DeleteAsync(AttachmentContent content, CancellationToken ct = default);
}

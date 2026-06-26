using MailArchiver.Models;
using MailArchiver.Utilities;

namespace MailArchiver.Services.Storage;

public class FileStorage : IAttachmentStorage
{
    private readonly string _basePath;
    private readonly ILogger<FileStorage> _logger;

    public FileStorage(string basePath, ILogger<FileStorage> logger)
    {
        _basePath = basePath;
        _logger = logger;
    }

    public AttachmentStorageType StorageType => AttachmentStorageType.File;

    public async Task<byte[]> ReadAsync(AttachmentContent content, CancellationToken ct = default)
    {
        var path = DerivePath(content);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Attachment file not found at '{path}' for content {content.Id}.");
        return await File.ReadAllBytesAsync(path, ct);
    }

    public async Task WriteAsync(AttachmentContent content, byte[] data, CancellationToken ct = default)
    {
        var path = DerivePath(content);
        var tmp = path + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(tmp, data, ct);
        File.Move(tmp, path, overwrite: true);
        _logger.LogDebug("Written content {Id} to {Path}.", content.Id, path);
    }

    public Task DeleteAsync(AttachmentContent content, CancellationToken ct = default)
    {
        var path = DerivePath(content);
        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.LogDebug("Deleted content {Id} at {Path}.", content.Id, path);
        }
        return Task.CompletedTask;
    }

    private string DerivePath(AttachmentContent content) => Path.Combine(_basePath, content.Hash);
}

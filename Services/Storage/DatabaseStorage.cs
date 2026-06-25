using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Services.Storage;

public class DatabaseStorage : IAttachmentStorage
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseStorage(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public AttachmentStorageType StorageType => AttachmentStorageType.Database;

    public Task<byte[]> ReadAsync(AttachmentContent content, CancellationToken ct = default)
    {
        var bytes = content.Content ?? Array.Empty<byte>();
        if (bytes.Length == 0 && content.Size != 0)
            throw new InvalidOperationException(
                $"AttachmentContent {content.Id} has no bytes in the database.");
        return Task.FromResult(bytes);
    }

    public async Task WriteAsync(AttachmentContent content, byte[] data, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
        var row = await context.AttachmentContents.FindAsync([content.Id], ct);
        if (row == null)
            throw new InvalidOperationException($"AttachmentContent {content.Id} not found.");
        row.Content = data;
        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(AttachmentContent content, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
        var row = await context.AttachmentContents.FindAsync([content.Id], ct);
        if (row != null)
        {
            row.Content = null;
            await context.SaveChangesAsync(ct);
        }
    }
}

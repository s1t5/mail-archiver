using MailArchiver.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MailArchiver.Services.Storage;

public class AttachmentStorageFactory
{
    private readonly IAttachmentStorage _databaseStorage;

    public AttachmentStorageFactory(IServiceScopeFactory scopeFactory)
    {
        _databaseStorage = new DatabaseStorage(scopeFactory);
    }

    public IAttachmentStorage ActiveStorage => _databaseStorage;

    public IAttachmentStorage GetStorageFor(AttachmentContent content) =>
        content.StorageType switch
        {
            AttachmentStorageType.Database => _databaseStorage,
            _ => throw new InvalidOperationException($"No storage configured for StorageType={content.StorageType}.")
        };
}

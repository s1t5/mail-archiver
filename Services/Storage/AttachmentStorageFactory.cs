using MailArchiver.Models;

namespace MailArchiver.Services.Storage;

public class AttachmentStorageFactory
{
    public const string StoragePathConfigKey = "Attachments:FilesStoragePath";
    public const string PreferredStorageConfigKey = "Attachments:PreferredStorage";

    private readonly IAttachmentStorage _databaseStorage;
    private readonly IAttachmentStorage? _fileStorage;
    private readonly IAttachmentStorage _activeStorage;

    public AttachmentStorageFactory(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<FileStorage> fileLogger)
    {
        _databaseStorage = new DatabaseStorage(scopeFactory);

        var basePath = configuration.GetValue<string>(StoragePathConfigKey);
        if (!string.IsNullOrWhiteSpace(basePath))
        {
            Directory.CreateDirectory(basePath);

            var probe = Path.Combine(basePath, ".write-probe");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);

            _fileStorage = new FileStorage(basePath, fileLogger);
        }

        var preferred = configuration.GetValue<string>(PreferredStorageConfigKey);
        _activeStorage = preferred?.ToLowerInvariant() switch
        {
            "database" => _databaseStorage,
            "file" => _fileStorage
                ?? throw new InvalidOperationException(
                    $"Preferred storage is 'file' but {StoragePathConfigKey} is not configured."),
            null => _fileStorage ?? _databaseStorage,
            _ => throw new InvalidOperationException(
                $"Unknown value '{preferred}' for {PreferredStorageConfigKey}. Valid values: 'database', 'file'.")
        };
    }

    public IAttachmentStorage ActiveStorage => _activeStorage;

    public IAttachmentStorage GetStorageFor(AttachmentContent content) =>
        content.StorageType switch
        {
            AttachmentStorageType.Database => _databaseStorage,
            AttachmentStorageType.File => _fileStorage
                ?? throw new InvalidOperationException(
                    $"Contents {content.Id} has StorageType=File but {StoragePathConfigKey} is not configured."),
            _ => throw new InvalidOperationException($"No storage configured for StorageType={content.StorageType}.")
        };
}

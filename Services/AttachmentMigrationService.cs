using MailArchiver.Data;
using MailArchiver.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MailArchiver.Services;

public class AttachmentMigrationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AttachmentStorageFactory _storageFactory;
    private readonly ILogger<AttachmentMigrationService> _logger;
    private readonly bool _migrationEnabled;
    private readonly int _batchSize;
    private readonly double _pauseSeconds;

    public AttachmentMigrationService(
        IServiceScopeFactory scopeFactory,
        AttachmentStorageFactory storageFactory,
        IConfiguration configuration,
        ILogger<AttachmentMigrationService> logger)
    {
        _scopeFactory = scopeFactory;
        _storageFactory = storageFactory;
        _logger = logger;
        _migrationEnabled = configuration.GetValue("Attachments:MigrationEnabled", true);
        _batchSize = configuration.GetValue("Attachments:MigrationBatchSize", 25);
        _pauseSeconds = configuration.GetValue("Attachments:MigrationPauseSeconds", 1.0);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_migrationEnabled)
        {
            _logger.LogInformation("Attachment migration is disabled.");
            return;
        }

        var target = _storageFactory.ActiveStorage;

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
            var pending = await db.AttachmentContents
                .CountAsync(c => c.StorageType != target.StorageType, ct);

            if (pending == 0) {
                _logger.LogInformation("No attachment content migration needed.");
                return;
            }

            _logger.LogInformation("Migrating {Count} attachment content(s) to {StorageType}.", pending, target.StorageType);
        }

        var failedIds = new HashSet<int>();

        while (!ct.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

            var batch = await db.AttachmentContents
                .Where(c => c.StorageType != target.StorageType && !failedIds.Contains(c.Id))
                .Take(_batchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
            {
                if (failedIds.Count > 0)
                    _logger.LogWarning("Attachment migration finished with errors. Failed content ID(s): {Ids}.",
                        string.Join(", ", failedIds));
                else
                    _logger.LogInformation("Attachment migration complete.");

                await VacuumAttachmentContentsAsync(ct);
                break;
            }

            foreach (var content in batch)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var source = _storageFactory.GetStorageFor(content);
                    var data = await source.ReadAsync(content, ct);

                    content.StorageType = target.StorageType;
                    await target.WriteAsync(content, data, ct);
                    await db.SaveChangesAsync(ct);

                    await source.DeleteAsync(content, ct);

                    _logger.LogDebug(
                        "Migrated content {ContentId} from {Source} to {Target}.",
                        content.Id, source.StorageType, target.StorageType);
                }
                catch (Exception ex)
                {
                    failedIds.Add(content.Id);
                    _logger.LogError(ex, "Failed to migrate content {ContentId}.", content.Id);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(_pauseSeconds), ct);
        }
    }

    private async Task VacuumAttachmentContentsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();
            // VACUUM must run outside a transaction; open the connection directly.
            var conn = (NpgsqlConnection)context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                await conn.OpenAsync(ct);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "VACUUM mail_archiver.\"AttachmentContents\"";
            await cmd.ExecuteNonQueryAsync(ct);
            _logger.LogInformation("Vacuumed AttachmentContents table.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to vacuum AttachmentContents table.");
        }
    }
}

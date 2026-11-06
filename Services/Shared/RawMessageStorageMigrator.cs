using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services.Shared
{
    /// <summary>Outcome of a storage migration run.</summary>
    public sealed record RawMessageMigrationResult(
        int Moved,
        int Skipped,
        int Failed,
        IReadOnlyList<string> Errors)
    {
        public bool Success => Failed == 0;
    }

    /// <summary>
    /// Moves stored original messages between the Postgres and filesystem backends.
    /// <para>
    /// Every message is read through its current backend, verified against the recorded
    /// checksum, written through the target backend and only then removed from the source. A
    /// message whose content does not match its checksum is left where it is and reported, so a
    /// corrupt original is never propagated and never silently dropped.
    /// </para>
    /// <para>
    /// The run is batched and resumable: candidates are selected by the storage column, so an
    /// interrupted run can simply be started again and continues with what is still on the
    /// source backend.
    /// </para>
    /// </summary>
    public class RawMessageStorageMigrator
    {
        private readonly MailArchiverDbContext _context;
        private readonly RawMessageOptions _options;
        private readonly RawMessagePathResolver _paths;
        private readonly ILogger<RawMessageStorageMigrator> _logger;

        public RawMessageStorageMigrator(
            MailArchiverDbContext context,
            IOptions<RawMessageOptions> options,
            RawMessagePathResolver paths,
            ILogger<RawMessageStorageMigrator> logger)
        {
            _context = context;
            _options = options.Value;
            _paths = paths;
            _logger = logger;
        }

        /// <summary>
        /// Moves up to <paramref name="batchSize"/> messages from <paramref name="from"/> to
        /// <paramref name="to"/>. Pass null for either to use the configured backend.
        /// </summary>
        public async Task<RawMessageMigrationResult> MigrateAsync(
            string? from = null,
            string? to = null,
            int batchSize = 200,
            CancellationToken cancellationToken = default)
        {
            var source = RawMessageStorage.Normalize(from ?? _options.EffectiveStorage);
            var target = RawMessageStorage.Normalize(to ?? _options.EffectiveStorage);

            if (source == target)
            {
                return new RawMessageMigrationResult(0, 0, 0, Array.Empty<string>());
            }

            var moved = 0;
            var failed = 0;
            var errors = new List<string>();

            var candidates = await _context.RawMessages
                .Where(r => r.StorageType == source)
                .OrderBy(r => r.ArchivedEmailId)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            // Files to remove once the batch is committed. Deleting them earlier would destroy
            // the only copy if the save below fails.
            var filesToDelete = new List<string>();

            foreach (var row in candidates)
            {
                try
                {
                    if (target == RawMessageStorage.FileSystem)
                    {
                        await MoveToFileSystemAsync(row, cancellationToken);
                    }
                    else
                    {
                        await MoveToPostgresAsync(row, cancellationToken, filesToDelete);
                    }

                    moved++;
                }
                catch (Exception ex) when (ex is RawMessageIntegrityException or IOException
                    or UnauthorizedAccessException or InvalidDataException)
                {
                    failed++;
                    errors.Add($"Email {row.ArchivedEmailId}: {ex.Message}");
                    _logger.LogError(ex,
                        "Raw message storage migration failed for email {EmailId}", row.ArchivedEmailId);
                }
            }

            if (moved > 0)
            {
                await _context.SaveChangesAsync(cancellationToken);

                // Only now is the database the authoritative copy of these messages.
                DeleteFiles(filesToDelete);
            }

            var skipped = candidates.Count - moved - failed;

            _logger.LogInformation(
                "Raw message storage migration {From} -> {To}: {Moved} moved, {Skipped} skipped, {Failed} failed",
                source, target, moved, skipped, failed);

            return new RawMessageMigrationResult(moved, skipped, failed, errors);
        }

        /// <summary>
        /// Number of messages still stored on a backend, i.e. the work a migration would have
        /// to do.
        /// </summary>
        public Task<int> CountAsync(string storage, CancellationToken cancellationToken = default)
        {
            var normalized = RawMessageStorage.Normalize(storage);
            return _context.RawMessages.CountAsync(r => r.StorageType == normalized, cancellationToken);
        }

        private async Task MoveToFileSystemAsync(RawMessage row, CancellationToken cancellationToken)
        {
            var bytes = RawMessageCodec.Decompress(row);

            // Verify before writing: a corrupt row must neither be copied nor deleted.
            var actual = RawMessageCodec.ComputeSha256(bytes);
            if (!string.Equals(actual, row.Sha256, StringComparison.Ordinal))
            {
                throw new RawMessageIntegrityException(row.ArchivedEmailId, row.Sha256, actual);
            }

            var relative = _paths.GetRelativePath(row.ArchivedEmailId);
            var absolute = _paths.GetAbsolutePath(relative);
            var directory = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = absolute + ".tmp";
            await File.WriteAllBytesAsync(temp, RawMessageCodec.Compress(bytes), cancellationToken);
            File.Move(temp, absolute, overwrite: true);

            // The column is cleared in the same save that flips the backend, so there is no
            // window in which the bytes exist nowhere.
            row.Content = null;
            row.StorageType = RawMessageStorage.FileSystem;
            row.StoragePath = relative;
        }

        private async Task MoveToPostgresAsync(
            RawMessage row, CancellationToken cancellationToken, List<string> filesToDelete)
        {
            var absolute = _paths.GetAbsolutePath(
                string.IsNullOrEmpty(row.StoragePath)
                    ? _paths.GetRelativePath(row.ArchivedEmailId)
                    : row.StoragePath);

            if (!File.Exists(absolute))
            {
                throw new FileNotFoundException(
                    $"the message file is missing at {absolute}", absolute);
            }

            var compressed = await File.ReadAllBytesAsync(absolute, cancellationToken);
            var bytes = RawMessageCodec.Decompress(compressed, row.Size);

            var actual = RawMessageCodec.ComputeSha256(bytes);
            if (!string.Equals(actual, row.Sha256, StringComparison.Ordinal))
            {
                throw new RawMessageIntegrityException(row.ArchivedEmailId, row.Sha256, actual);
            }

            row.Content = compressed;
            row.StorageType = RawMessageStorage.Postgres;
            row.StoragePath = null;

            filesToDelete.Add(absolute);
        }

        private void DeleteFiles(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex,
                        "Moved raw message into the database but could not delete {Path}", path);
                }
            }
        }
    }
}

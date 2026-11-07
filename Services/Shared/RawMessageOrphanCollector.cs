using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Removes original message files that no longer belong to any email.
    /// <para>
    /// The delete paths remove a message's file together with its row, but the two cannot be
    /// committed atomically: the file lives outside the database. Anything that interrupts the
    /// pair - a crash, a permission problem, a row deleted by hand, or a retention run from
    /// before this cleanup existed - leaves a file behind. This collector is the net under all
    /// of them: it trusts only the actual reference state, exactly like the attachment orphan
    /// cleanup does.
    /// </para>
    /// <para>
    /// Only files older than the grace period are removed, so an import that has just written a
    /// file but not yet committed its row is never collected.
    /// </para>
    /// </summary>
    public class RawMessageOrphanCollector
    {
        private readonly MailArchiverDbContext _context;
        private readonly RawMessagePathResolver _paths;
        private readonly RawMessageOptions _options;
        private readonly ILogger<RawMessageOrphanCollector> _logger;

        public RawMessageOrphanCollector(
            MailArchiverDbContext context,
            RawMessagePathResolver paths,
            IOptions<RawMessageOptions> options,
            ILogger<RawMessageOrphanCollector> logger)
        {
            _context = context;
            _paths = paths;
            _options = options.Value;
            _logger = logger;
        }

        /// <summary>Result of one collection run.</summary>
        public sealed record Result(int Scanned, int Removed, int Kept);

        /// <summary>
        /// Walks the base directory once and deletes every file that no row points at.
        /// </summary>
        public async Task<Result> CollectAsync(TimeSpan grace, CancellationToken cancellationToken)
        {
            var basePath = _paths.BasePath;
            if (!Directory.Exists(basePath))
            {
                return new Result(0, 0, 0);
            }

            // Every path the database still expects, normalised the way the writer stored it.
            var referenced = await _context.RawMessages
                .AsNoTracking()
                .Where(r => r.StorageType == RawMessageStorage.FileSystem)
                .Select(r => new { r.StoragePath, r.ArchivedEmailId })
                .ToListAsync(cancellationToken);

            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in referenced)
            {
                var relative = string.IsNullOrEmpty(row.StoragePath)
                    ? _paths.GetRelativePath(row.ArchivedEmailId)
                    : row.StoragePath;
                known.Add(relative);
            }

            var cutoff = DateTime.UtcNow - grace;
            var scanned = 0;
            var removed = 0;
            var kept = 0;

            foreach (var file in Directory.EnumerateFiles(basePath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                scanned++;

                var relative = Path.GetRelativePath(basePath, file).Replace('\\', '/');

                // A file written moments ago may belong to an import that has not committed
                // its row yet; a .tmp file is a write in progress.
                if (known.Contains(relative))
                {
                    kept++;
                    continue;
                }

                if (relative.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                {
                    // Half-written leftovers are only touched once they are clearly abandoned.
                    if (File.GetLastWriteTimeUtc(file) > cutoff)
                    {
                        kept++;
                        continue;
                    }
                }
                else if (File.GetLastWriteTimeUtc(file) > cutoff)
                {
                    kept++;
                    continue;
                }

                try
                {
                    File.Delete(file);
                    removed++;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Could not remove orphaned original message file {Path}", file);
                }
            }

            if (removed > 0)
            {
                _logger.LogInformation(
                    "Removed {Removed} orphaned original message file(s) that no email refers to " +
                    "({Kept} referenced, {Scanned} scanned)", removed, kept, scanned);
            }

            return new Result(scanned, removed, kept);
        }
    }
}

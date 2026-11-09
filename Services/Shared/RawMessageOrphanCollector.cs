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

            // Files are checked against the database by email id, not by loading every row: an
            // archive with a million messages would otherwise hold a million paths in memory for
            // a walk that only needs to answer "does this id still have an original?".
            var cutoff = DateTime.UtcNow - grace;
            var scanned = 0;
            var removed = 0;
            var kept = 0;

            // Ids of the candidates in the current batch, checked in one query.
            const int batchSize = 5000;
            var pending = new List<(int EmailId, string Path)>(batchSize);

            async Task FlushAsync()
            {
                if (pending.Count == 0)
                {
                    return;
                }

                var ids = pending.Select(p => p.EmailId).Distinct().ToList();
                var alive = await _context.RawMessages
                    .AsNoTracking()
                    .Where(r => r.StorageType == RawMessageStorage.FileSystem && ids.Contains(r.ArchivedEmailId))
                    .Select(r => r.ArchivedEmailId)
                    .ToListAsync(cancellationToken);

                var stillReferenced = new HashSet<int>(alive);
                foreach (var (emailId, path) in pending)
                {
                    if (stillReferenced.Contains(emailId))
                    {
                        kept++;
                        continue;
                    }

                    try
                    {
                        File.Delete(path);
                        removed++;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        _logger.LogWarning(ex, "Could not remove orphaned original message file {Path}", path);
                    }
                }

                pending.Clear();
            }

            foreach (var file in Directory.EnumerateFiles(basePath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                scanned++;

                // A file written moments ago may belong to an import that has not committed its
                // row yet; a .tmp file is a write in progress and follows the same rule.
                if (File.GetLastWriteTimeUtc(file) > cutoff)
                {
                    kept++;
                    continue;
                }

                if (!TryReadEmailId(file, out var emailId))
                {
                    // Not a file this feature wrote; leave it alone rather than guess.
                    kept++;
                    continue;
                }

                pending.Add((emailId, file));
                if (pending.Count >= batchSize)
                {
                    await FlushAsync();
                }
            }

            await FlushAsync();

            if (removed > 0)
            {
                _logger.LogInformation(
                    "Removed {Removed} orphaned original message file(s) that no email refers to " +
                    "({Kept} referenced, {Scanned} scanned)", removed, kept, scanned);
            }

            return new Result(scanned, removed, kept);
        }

        /// <summary>
        /// Reads the email id out of a stored file name (<c>1234.eml.gz</c>). Matching on the id
        /// rather than on the full path keeps the check correct after the shard depth changed.
        /// </summary>
        private static bool TryReadEmailId(string path, out int emailId)
        {
            emailId = 0;

            var name = Path.GetFileName(path);
            if (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^4];
            }

            if (!name.EndsWith(".eml.gz", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return int.TryParse(name[..^7], out emailId);
        }
    }
}

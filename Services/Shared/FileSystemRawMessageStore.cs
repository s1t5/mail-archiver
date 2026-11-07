using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Stores original messages as gzip files next to the archive; the metadata row
    /// (checksum, size, capture source, relative path) stays in Postgres.
    /// </summary>
    public class FileSystemRawMessageStore : IRawMessageStore
    {
        private readonly MailArchiverDbContext _context;
        private readonly RawMessageOptions _options;
        private readonly RawMessagePathResolver _paths;
        private readonly ILogger<FileSystemRawMessageStore> _logger;

        public FileSystemRawMessageStore(
            MailArchiverDbContext context,
            IOptions<RawMessageOptions> options,
            RawMessagePathResolver paths,
            ILogger<FileSystemRawMessageStore> logger)
        {
            _context = context;
            _options = options.Value;
            _paths = paths;
            _logger = logger;
        }

        public string StorageType => RawMessageStorage.FileSystem;

        public RawMessage? CreateRow(int emailId, RawMessageCapture? capture)
            => RawMessageCodec.CreateFileSystemBacked(capture, _options, _paths.GetRelativePath(emailId));

        /// <summary>
        /// Writes the message file. The row already carries the path, so a failure here leaves a
        /// row without a file; <see cref="ReadAsync"/> reports that as missing rather than
        /// serving something wrong.
        /// </summary>
        public async Task PersistAsync(RawMessage row, RawMessageCapture? capture)
        {
            if (!row.IsFileSystemBacked || capture == null || string.IsNullOrEmpty(row.StoragePath))
            {
                return;
            }

            var absolute = _paths.GetAbsolutePath(row.StoragePath);
            var directory = Path.GetDirectoryName(absolute);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var compressed = RawMessageCodec.Compress(capture.Bytes);

            // Write to a temporary name and move into place, so an interrupted import cannot
            // leave a half-written file where a complete one is expected.
            var temp = absolute + ".tmp";
            await File.WriteAllBytesAsync(temp, compressed);
            File.Move(temp, absolute, overwrite: true);
        }

        public async Task<bool> ExistsAsync(int emailId)
        {
            var row = await _context.RawMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ArchivedEmailId == emailId);

            if (row == null)
            {
                return false;
            }

            // A row whose file vanished is not a stored original; report it as absent so the
            // caller falls back to the rebuilt .eml instead of hitting a 500.
            return File.Exists(ResolvePath(row));
        }

        public async Task<RawMessageContent?> ReadAsync(int emailId)
        {
            var row = await _context.RawMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ArchivedEmailId == emailId);

            if (row == null)
            {
                return null;
            }

            var absolute = ResolvePath(row);
            if (!File.Exists(absolute))
            {
                _logger.LogWarning(
                    "Original message file for email {EmailId} is missing at {Path}", emailId, absolute);
                return null;
            }

            var compressed = await File.ReadAllBytesAsync(absolute);
            var bytes = RawMessageCodec.Decompress(compressed, row.Size);

            if (_options.FileSystem.VerifyChecksumOnRead)
            {
                var actual = RawMessageCodec.ComputeSha256(bytes);
                if (!string.Equals(actual, row.Sha256, StringComparison.Ordinal))
                {
                    _logger.LogError(
                        "Checksum mismatch for original message of email {EmailId} at {Path}: " +
                        "recorded {Expected}, file has {Actual}",
                        emailId, absolute, row.Sha256, actual);
                    throw new RawMessageIntegrityException(emailId, row.Sha256, actual);
                }
            }

            return new RawMessageContent(bytes, row.Sha256, row.Source, row.StorageType);
        }

        public async Task<string?> VerifyAsync(int emailId)
        {
            var row = await _context.RawMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ArchivedEmailId == emailId);

            if (row == null)
            {
                return null;
            }

            var absolute = ResolvePath(row);
            if (!File.Exists(absolute))
            {
                return $"file missing at {absolute}";
            }

            var bytes = RawMessageCodec.Decompress(await File.ReadAllBytesAsync(absolute), row.Size);
            var actual = RawMessageCodec.ComputeSha256(bytes);

            return string.Equals(actual, row.Sha256, StringComparison.Ordinal)
                ? null
                : $"checksum mismatch (recorded {row.Sha256}, file has {actual})";
        }

        /// <summary>
        /// Absolute path of a row's file. Falls back to the path derived from the email id when
        /// the column is empty, which keeps a row readable after a manual database fix-up.
        /// </summary>
        private string ResolvePath(RawMessage row)
            => _paths.GetAbsolutePath(
                string.IsNullOrEmpty(row.StoragePath) ? _paths.GetRelativePath(row.ArchivedEmailId) : row.StoragePath);

        /// <summary>
        /// Deletes the file, then the row. File first: an interruption between the two leaves a
        /// row pointing at nothing, which <see cref="ExistsAsync"/> reports as absent and which
        /// a repeated call cleans up. The other order would leave a file no row can find.
        /// </summary>
        public async Task DeleteAsync(int emailId)
        {
            var row = await _context.RawMessages
                .FirstOrDefaultAsync(r => r.ArchivedEmailId == emailId);

            if (row == null)
            {
                return;
            }

            if (row.IsFileSystemBacked)
            {
                var absolute = ResolvePath(row);
                try
                {
                    if (File.Exists(absolute))
                    {
                        File.Delete(absolute);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Leave the row in place: the file still exists, and the orphan collector
                    // picks both up later. Deleting the row here would strand the bytes for good.
                    _logger.LogWarning(ex,
                        "Could not delete original message file for email {EmailId} at {Path}; " +
                        "keeping its row so the file stays traceable", emailId, absolute);
                    return;
                }
            }

            _context.RawMessages.Remove(row);
            await _context.SaveChangesAsync();
        }
    }
}

using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Stores original messages in the <c>RawMessages.Content</c> column, the behaviour the
    /// archive has always had. Reads tolerate rows that a migration moved to the filesystem.
    /// </summary>
    public class PostgresRawMessageStore : IRawMessageStore
    {
        private readonly MailArchiverDbContext _context;
        private readonly RawMessageOptions _options;

        public PostgresRawMessageStore(MailArchiverDbContext context, IOptions<RawMessageOptions> options)
        {
            _context = context;
            _options = options.Value;
        }

        public string StorageType => RawMessageStorage.Postgres;

        public RawMessage? CreateRow(int emailId, RawMessageCapture? capture)
            => RawMessageCodec.Create(capture, _options);

        /// <summary>Nothing to do: the bytes travel inside the row.</summary>
        public Task PersistAsync(RawMessage row, RawMessageCapture? capture) => Task.CompletedTask;

        public Task<bool> ExistsAsync(int emailId)
            => _context.RawMessages.AsNoTracking().AnyAsync(r => r.ArchivedEmailId == emailId);

        public async Task<RawMessageContent?> ReadAsync(int emailId)
        {
            var row = await _context.RawMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ArchivedEmailId == emailId);

            if (row == null || row.IsFileSystemBacked)
            {
                // A filesystem-backed row is not readable through this store; the caller asked
                // for a Postgres row that is not there.
                return null;
            }

            var bytes = RawMessageCodec.Decompress(row);
            var actual = RawMessageCodec.ComputeSha256(bytes);

            if (!string.Equals(actual, row.Sha256, StringComparison.Ordinal))
            {
                throw new RawMessageIntegrityException(emailId, row.Sha256, actual);
            }

            return new RawMessageContent(bytes, row.Sha256, row.Source, row.StorageType);
        }

        public async Task<string?> VerifyAsync(int emailId)
        {
            var row = await _context.RawMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ArchivedEmailId == emailId);

            if (row == null || row.IsFileSystemBacked)
            {
                return null;
            }

            var bytes = RawMessageCodec.Decompress(row);
            var actual = RawMessageCodec.ComputeSha256(bytes);

            return string.Equals(actual, row.Sha256, StringComparison.Ordinal)
                ? null
                : $"checksum mismatch (recorded {row.Sha256}, row has {actual})";
        }

        /// <summary>
        /// Removes the row; the bytes live inside it. Filesystem-backed rows are left alone,
        /// their bytes are not reachable from here.
        /// </summary>
        public async Task DeleteAsync(int emailId)
        {
            var row = await _context.RawMessages
                .FirstOrDefaultAsync(r => r.ArchivedEmailId == emailId);

            if (row == null || row.IsFileSystemBacked)
            {
                return;
            }

            _context.RawMessages.Remove(row);
            await _context.SaveChangesAsync();
        }
    }
}

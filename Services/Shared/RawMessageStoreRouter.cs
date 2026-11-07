using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Entry point the application talks to. Writes go to the configured backend; reads follow
    /// the storage recorded on the row, so an archive that was migrated - or is mid-migration -
    /// keeps serving every message regardless of where it currently lives.
    /// </summary>
    public class RawMessageStoreRouter : IRawMessageStore
    {
        private readonly MailArchiverDbContext _context;
        private readonly RawMessageOptions _options;
        private readonly PostgresRawMessageStore _postgres;
        private readonly FileSystemRawMessageStore _fileSystem;

        public RawMessageStoreRouter(
            MailArchiverDbContext context,
            IOptions<RawMessageOptions> options,
            PostgresRawMessageStore postgres,
            FileSystemRawMessageStore fileSystem)
        {
            _context = context;
            _options = options.Value;
            _postgres = postgres;
            _fileSystem = fileSystem;
        }

        /// <summary>The backend writes currently go to.</summary>
        public string StorageType => _options.EffectiveStorage;

        private IRawMessageStore ActiveWriter =>
            _options.WritesToFileSystem ? _fileSystem : _postgres;

        public RawMessage? CreateRow(int emailId, RawMessageCapture? capture)
            => ActiveWriter.CreateRow(emailId, capture);

        public Task PersistAsync(RawMessage row, RawMessageCapture? capture)
            => WriterFor(row).PersistAsync(row, capture);

        public async Task<bool> ExistsAsync(int emailId)
        {
            var row = await FindRowAsync(emailId);
            return row != null && await WriterFor(row).ExistsAsync(emailId);
        }

        public async Task<RawMessageContent?> ReadAsync(int emailId)
        {
            var row = await FindRowAsync(emailId);
            return row == null ? null : await WriterFor(row).ReadAsync(emailId);
        }

        public async Task<string?> VerifyAsync(int emailId)
        {
            var row = await FindRowAsync(emailId);
            return row == null ? null : await WriterFor(row).VerifyAsync(emailId);
        }

        /// <summary>
        /// Deletes the files of the given emails. Always the filesystem store: after the email
        /// rows are gone there is no storage type left to read, and asking for a file that does
        /// not exist is a no-op, so a Postgres-only archive is unaffected.
        /// </summary>
        public Task DeleteAsync(IReadOnlyCollection<int> emailIds, CancellationToken cancellationToken = default)
            => _fileSystem.DeleteAsync(emailIds, cancellationToken);

        private IRawMessageStore WriterFor(RawMessage row)
            => row.IsFileSystemBacked ? _fileSystem : _postgres;

        private Task<RawMessage?> FindRowAsync(int emailId)
            => _context.RawMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ArchivedEmailId == emailId);
    }
}

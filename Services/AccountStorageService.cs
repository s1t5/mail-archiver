using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace MailArchiver.Services
{
    public class AccountStorageService : IAccountStorageService
    {
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<AccountStorageService> _logger;
        private readonly IConfiguration _configuration;

        public AccountStorageService(
            MailArchiverDbContext context,
            ILogger<AccountStorageService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<Dictionary<int, string>> GetStorageForAccountsAsync(IEnumerable<int> accountIds)
        {
            var idList = accountIds.ToList();
            if (idList.Count == 0)
                return new Dictionary<int, string>();

            var cached = await _context.AccountStorageCaches
                .Where(c => idList.Contains(c.MailAccountId))
                .ToDictionaryAsync(c => c.MailAccountId, c => FormatFileSize(c.TotalBytes));

            // Accounts ohne Cache-Eintrag -> "0 B"
            var result = new Dictionary<int, string>();
            foreach (var id in idList)
            {
                result[id] = cached.TryGetValue(id, out var val) ? val : FormatFileSize(0);
            }
            return result;
        }

        public async Task<bool> RefreshAccountStorageAsync(int mailAccountId)
        {
            try
            {
                var (mailBytes, attachmentBytes) = await CalculateAccountStorageAsync(mailAccountId);
                var totalBytes = mailBytes + attachmentBytes;

                await UpsertCacheAsync(mailAccountId, mailBytes, attachmentBytes, totalBytes);
                await EnsureBackfillStateDoneAsync(mailAccountId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing storage for account {AccountId}", mailAccountId);
                return false;
            }
        }

        public async Task RefreshAllAccountStorageAsync(CancellationToken ct = default)
        {
            var batchDelayMs = _configuration.GetValue<int>("AccountStorage:RefreshBatchDelayMs", 1000);

            var accountIds = await _context.MailAccounts
                .Select(a => a.Id)
                .ToListAsync(ct);

            _logger.LogInformation("Starting full storage refresh for {Count} accounts", accountIds.Count);

            int succeeded = 0;
            int failed = 0;

            foreach (var accountId in accountIds)
            {
                if (ct.IsCancellationRequested)
                    break;

                if (await RefreshAccountStorageAsync(accountId))
                    succeeded++;
                else
                    failed++;

                if (batchDelayMs > 0)
                    await Task.Delay(batchDelayMs, ct);
            }

            if (failed > 0)
                _logger.LogWarning("Full storage refresh completed ({Succeeded} succeeded, {Failed} failed)", succeeded, failed);
            else
                _logger.LogInformation("Full storage refresh completed ({Succeeded} succeeded, {Failed} failed)", succeeded, failed);
        }

        public async Task EnsureBackfillStatesAsync()
        {
            var existingIds = await _context.AccountStorageBackfillStates
                .Select(s => s.MailAccountId)
                .ToListAsync();

            var allAccountIds = await _context.MailAccounts
                .Select(a => a.Id)
                .ToListAsync();

            var missing = allAccountIds.Except(existingIds).ToList();
            if (missing.Count == 0)
                return;

            foreach (var id in missing)
            {
                _context.AccountStorageBackfillStates.Add(new AccountStorageBackfillState
                {
                    MailAccountId = id,
                    Status = "Pending"
                });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Created {Count} backfill state entries", missing.Count);
        }

        /// <summary>
        /// Berechnet MailBytes (pg_column_size der Text/Bytea-Spalten) und
        /// AttachmentBytes (logische Summe EmailAttachment.Size) fuer einen Account.
        /// </summary>
        private async Task<(long mailBytes, long attachmentBytes)> CalculateAccountStorageAsync(int mailAccountId)
        {
            var connectionString = _context.Database.GetConnectionString();

            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            // Hinweis: Der Npgsql-Default-CommandTimeout betraegt 30 Sekunden und ist
            // fuer grosse Archive zu knapp. Der Timeout ist daher konfigurierbar, analog zu
            // AttachmentDeduplication:CommandTimeoutSeconds.
            var commandTimeoutSeconds = _configuration.GetValue<int>("AccountStorage:CommandTimeoutSeconds", 300);

            using var command = new NpgsqlCommand(BuildStorageSql(_context.Model), connection);
            command.CommandTimeout = commandTimeoutSeconds;
            command.Parameters.AddWithValue("@accountId", mailAccountId);
            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var mailBytes = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                var attachmentBytes = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                return (mailBytes, attachmentBytes);
            }

            return (0, 0);
        }

        /// <summary>
        /// Builds the storage query. MailBytes adds up pg_column_size for each column of
        /// ArchivedEmails separately instead of calling it on the whole row.
        /// </summary>
        /// <remarks>
        /// pg_column_size(e) makes PostgreSQL turn the row into a composite value, and that
        /// fetches every TOASTed column first, so it read every mail body of the account on
        /// each refresh. On a single column the size comes from the TOAST pointer and nothing
        /// is fetched, so only the table itself is scanned. The result is the same apart from
        /// the row header. The columns come from the EF model so new ones are counted too.
        /// The COALESCE subqueries return 0 for accounts without emails or attachments.
        /// </remarks>
        internal static string BuildStorageSql(IModel model)
        {
            var columnSizes = model.FindEntityType(typeof(ArchivedEmail))!
                .GetProperties()
                .Select(p => $@"COALESCE(pg_column_size(e.""{p.GetColumnName()}""), 0)::bigint");

            return $@"
                SELECT
                    COALESCE((
                        SELECT SUM({string.Join(" + ", columnSizes)})
                        FROM mail_archiver.""ArchivedEmails"" e
                        WHERE e.""MailAccountId"" = @accountId
                    ), 0) AS MailBytes,
                    COALESCE((
                        SELECT SUM(a.""Size"")
                        FROM mail_archiver.""EmailAttachments"" a
                        JOIN mail_archiver.""ArchivedEmails"" e2 ON a.""ArchivedEmailId"" = e2.""Id""
                        WHERE e2.""MailAccountId"" = @accountId
                    ), 0) AS AttachmentBytes;
            ";
        }

        private async Task UpsertCacheAsync(int mailAccountId, long mailBytes, long attachmentBytes, long totalBytes)
        {
            var sql = @"
                INSERT INTO mail_archiver.""AccountStorageCache""
                    (""MailAccountId"", ""MailBytes"", ""AttachmentBytes"", ""TotalBytes"", ""UpdatedAt"")
                VALUES
                    (@accountId, @mailBytes, @attachmentBytes, @totalBytes, now())
                ON CONFLICT (""MailAccountId"")
                DO UPDATE SET
                    ""MailBytes"" = EXCLUDED.""MailBytes"",
                    ""AttachmentBytes"" = EXCLUDED.""AttachmentBytes"",
                    ""TotalBytes"" = EXCLUDED.""TotalBytes"",
                    ""UpdatedAt"" = now();
            ";

            var connectionString = _context.Database.GetConnectionString();
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@accountId", mailAccountId);
            command.Parameters.AddWithValue("@mailBytes", mailBytes);
            command.Parameters.AddWithValue("@attachmentBytes", attachmentBytes);
            command.Parameters.AddWithValue("@totalBytes", totalBytes);
            await command.ExecuteNonQueryAsync();
        }

        private async Task EnsureBackfillStateDoneAsync(int mailAccountId)
        {
            var sql = @"
                INSERT INTO mail_archiver.""AccountStorageBackfillState""
                    (""MailAccountId"", ""Status"", ""CompletedAt"")
                VALUES
                    (@accountId, 'Done', now())
                ON CONFLICT (""MailAccountId"")
                DO UPDATE SET
                    ""Status"" = 'Done',
                    ""CompletedAt"" = now();
            ";

            var connectionString = _context.Database.GetConnectionString();
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@accountId", mailAccountId);
            await command.ExecuteNonQueryAsync();
        }

        public static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }
    }
}

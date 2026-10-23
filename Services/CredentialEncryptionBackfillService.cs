using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services
{
    /// <summary>
    /// One-time (resumable) background migration that encrypts existing plain text mail
    /// account credentials once a key encryption key has been configured.
    ///
    /// It selects accounts that still carry a plain text value in any credential column,
    /// marks those properties as modified and saves the entity. The EF value converter
    /// encrypts the values on write. The migration is idempotent: already encrypted rows
    /// are skipped by the selection query, so an interrupted run simply continues later.
    ///
    /// Without a configured key (or when <c>Security:CredentialEncryptionBackfill</c> is
    /// false) the service is a no-op, leaving existing plain text untouched.
    /// </summary>
    public class CredentialEncryptionBackfillService : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);
        private const int BatchSize = 100;
        private static readonly TimeSpan DelayBetweenBatches = TimeSpan.FromMilliseconds(200);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ICredentialProtector _credentialProtector;
        private readonly SecurityOptions _options;
        private readonly ILogger<CredentialEncryptionBackfillService> _logger;

        public CredentialEncryptionBackfillService(
            IServiceScopeFactory scopeFactory,
            ICredentialProtector credentialProtector,
            IOptions<SecurityOptions> options,
            ILogger<CredentialEncryptionBackfillService> logger)
        {
            _scopeFactory = scopeFactory;
            _credentialProtector = credentialProtector;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.CredentialEncryptionBackfill)
            {
                _logger.LogInformation("Credential encryption backfill is disabled (Security:CredentialEncryptionBackfill=false)");
                return;
            }

            if (!_credentialProtector.IsEnabled)
            {
                _logger.LogInformation("Credential encryption backfill skipped: no encryption key configured");
                return;
            }

            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await RunBackfillAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Credential encryption backfill cancelled; will resume on next start");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Credential encryption backfill failed: {Message}. It will resume on the next application start.",
                    ex.Message);
            }
        }

        private async Task RunBackfillAsync(CancellationToken token)
        {
            long total = 0;
            List<int>? previousIds = null;

            while (!token.IsCancellationRequested)
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

                var ids = await FindPlaintextIdsAsync(context, token);
                if (ids.Count == 0)
                    break;

                // Protect() falls back to plain text when encryption fails. If the same
                // batch of ids is selected again, no row was encrypted in this process
                // (e.g. AES-GCM is unsupported on this platform). Abort instead of
                // spinning forever; the rows are retried on the next application start.
                if (previousIds != null && ids.SequenceEqual(previousIds))
                    throw new InvalidOperationException(
                        "Credential encryption backfill made no progress; encryption appears to be persistently failing. " +
                        "Check the encryption key configuration and the application log. The backfill will resume on the next application start.");

                previousIds = ids;

                // Fewer ids than the batch size means the query found everything remaining.
                var lastBatch = ids.Count < BatchSize;

                var accounts = await context.MailAccounts
                    .Where(a => ids.Contains(a.Id))
                    .ToListAsync(token);

                foreach (var account in accounts)
                {
                    var entry = context.Entry(account);
                    MarkModified(entry, account.Password, nameof(MailAccount.Password));
                    MarkModified(entry, account.ClientSecret, nameof(MailAccount.ClientSecret));
                    MarkModified(entry, account.OAuthRefreshToken, nameof(MailAccount.OAuthRefreshToken));
                    MarkModified(entry, account.OAuthAccessToken, nameof(MailAccount.OAuthAccessToken));
                }

                await context.SaveChangesAsync(token);
                total += accounts.Count;

                _logger.LogInformation("Credential encryption backfill progress: {Total} account(s) encrypted", total);

                if (lastBatch)
                    break;

                try
                {
                    await Task.Delay(DelayBetweenBatches, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            if (!token.IsCancellationRequested && total > 0)
            {
                _logger.LogInformation("Credential encryption backfill COMPLETED: {Total} account(s) encrypted", total);
            }
        }

        internal static async Task<List<int>> FindPlaintextIdsAsync(MailArchiverDbContext context, CancellationToken token)
        {
            // "enc:%" matches the encryption envelope prefix; any non-null value that does not
            // start with it is still plain text and needs to be encrypted. Empty strings are
            // excluded: the protector passes them through unencrypted, so selecting them would
            // make the batch loop spin forever without ever writing anything.
            const string sql = @"
                SELECT ""Id""
                FROM mail_archiver.""MailAccounts""
                WHERE (""Password"" IS NOT NULL AND ""Password"" <> '' AND ""Password"" NOT LIKE 'enc:%')
                   OR (""ClientSecret"" IS NOT NULL AND ""ClientSecret"" <> '' AND ""ClientSecret"" NOT LIKE 'enc:%')
                   OR (""OAuthRefreshToken"" IS NOT NULL AND ""OAuthRefreshToken"" <> '' AND ""OAuthRefreshToken"" NOT LIKE 'enc:%')
                   OR (""OAuthAccessToken"" IS NOT NULL AND ""OAuthAccessToken"" <> '' AND ""OAuthAccessToken"" NOT LIKE 'enc:%')
                ORDER BY ""Id""
                LIMIT {0};";

            return await context.Database.SqlQueryRaw<int>(sql, BatchSize).ToListAsync(token);
        }

        private static void MarkModified(
            Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<MailAccount> entry,
            string? value,
            string propertyName)
        {
            if (!string.IsNullOrEmpty(value))
                entry.Property(propertyName).IsModified = true;
        }
    }
}

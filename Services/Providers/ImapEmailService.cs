using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Core;
using MailArchiver.Services.Providers.Imap;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Services.Providers
{
    /// <summary>
    /// Facade that implements IProviderEmailService by delegating to specialized
    /// IMAP service classes: ImapMailSyncService, ImapMailRestorer, ImapConnectionFactory,
    /// and IImapFolderService.
    /// </summary>
    public class ImapEmailService : IProviderEmailService
    {
        private readonly ImapMailSyncService _syncService;
        private readonly ImapMailRestorer _restorer;
        private readonly ImapConnectionFactory _connectionFactory;
        private readonly IImapFolderService _folderService;
        private readonly EmailCoreService _coreService;
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<ImapEmailService> _logger;

        public ImapEmailService(
            ImapMailSyncService syncService,
            ImapMailRestorer restorer,
            ImapConnectionFactory connectionFactory,
            IImapFolderService folderService,
            EmailCoreService coreService,
            MailArchiverDbContext context,
            ILogger<ImapEmailService> logger)
        {
            _syncService = syncService;
            _restorer = restorer;
            _connectionFactory = connectionFactory;
            _folderService = folderService;
            _coreService = coreService;
            _context = context;
            _logger = logger;
        }

        // ========================================
        // IProviderEmailService
        // ========================================

        public Task SyncMailAccountAsync(MailAccount account, string? jobId = null)
            => _syncService.SyncMailAccountAsync(account, jobId);

        public async Task<bool> TestConnectionAsync(MailAccount account)
            => await _syncService.TestConnectionAsync(account);

        public async Task<List<string>> GetMailFoldersAsync(int accountId)
        {
            var account = await _context.MailAccounts.FindAsync(accountId);
            if (account == null)
                return new List<string>();

            try
            {
                using var client = _connectionFactory.CreateImapClient(account.Name);
                client.Timeout = 30000;
                client.ServerCertificateValidationCallback = _connectionFactory.ServerCertificateValidationCallback;

                await _connectionFactory.ConnectWithFallbackAsync(client, account.ImapServer, account.ImapPort ?? 993, account.UseSSL, account.Name);
                await _connectionFactory.AuthenticateClientAsync(client, account);

                var folders = await _folderService.GetAllFoldersAsync(client, account.Name);
                var folderNames = folders.Select(f => f.FullName).ToList();

                await client.DisconnectAsync(true);
                return folderNames;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mail folders for account {AccountName}", account.Name);
                return new List<string>();
            }
        }

        public async Task<bool> RestoreEmailToFolderAsync(int emailId, int targetAccountId, string folderName)
        {
            return await _restorer.RestoreEmailToFolderAsync(emailId, targetAccountId, folderName, false);
        }

        public async Task<bool> RestoreEmailToFolderAsync(int emailId, int targetAccountId, string folderName, bool preserveFolderStructure)
        {
            return await _restorer.RestoreEmailToFolderAsync(emailId, targetAccountId, folderName, preserveFolderStructure);
        }

        public Task<(int Successful, int Failed)> RestoreMultipleEmailsWithProgressAsync(
            List<int> emailIds,
            int targetAccountId,
            string folderName,
            Action<int, int, int> progressCallback,
            CancellationToken cancellationToken)
        {
            return RestoreMultipleEmailsWithProgressAsync(emailIds, targetAccountId, folderName, false, progressCallback, cancellationToken);
        }

        public async Task<(int Successful, int Failed)> RestoreMultipleEmailsWithProgressAsync(
            List<int> emailIds,
            int targetAccountId,
            string folderName,
            bool preserveFolderStructure,
            Action<int, int, int> progressCallback,
            CancellationToken cancellationToken)
        {
            if (preserveFolderStructure)
            {
                return await _restorer.RestoreMultipleEmailsWithFolderStructureAsync(
                    emailIds, targetAccountId, folderName, progressCallback, cancellationToken);
            }

            return await _restorer.RestoreMultipleEmailsWithSharedConnectionAndProgressAsync(
                emailIds, targetAccountId, folderName, progressCallback, cancellationToken);
        }

        public async Task<bool> ResyncAccountAsync(int accountId)
        {
            return await _syncService.ResyncAccountAsync(accountId);
        }

        public Task<int> GetEmailCountByAccountAsync(int accountId)
        {
            return _coreService.GetEmailCountByAccountAsync(accountId);
        }
    }
}
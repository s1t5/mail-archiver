using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Core;
using MailArchiver.Services.Providers.Graph;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Services.Providers
{
    /// <summary>
    /// Facade that implements both IGraphEmailService and IProviderEmailService by delegating
    /// to specialized graph service classes: GraphMailSyncService, GraphMailRestorer,
    /// GraphFolderService, and GraphAuthClientFactory.
    /// </summary>
    public class GraphEmailService : IGraphEmailService, IProviderEmailService
    {
        private readonly GraphMailSyncService _syncService;
        private readonly GraphMailRestorer _restorer;
        private readonly GraphAuthClientFactory _authFactory;
        private readonly IGraphFolderService _folderService;
        private readonly EmailCoreService _coreService;
        private readonly MailArchiverDbContext _context;

        public GraphEmailService(
            GraphMailSyncService syncService,
            GraphMailRestorer restorer,
            GraphAuthClientFactory authFactory,
            IGraphFolderService folderService,
            EmailCoreService coreService,
            MailArchiverDbContext context)
        {
            _syncService = syncService;
            _restorer = restorer;
            _authFactory = authFactory;
            _folderService = folderService;
            _coreService = coreService;
            _context = context;
        }

        // ========================================
        // IGraphEmailService
        // ========================================

        public Task SyncMailAccountAsync(MailAccount account, string? jobId = null)
            => _syncService.SyncMailAccountAsync(account, jobId);

        public Task<bool> TestConnectionAsync(MailAccount account)
            => _syncService.TestConnectionAsync(account);

        public async Task<List<string>> GetMailFoldersAsync(MailAccount account)
            => await _folderService.GetMailFoldersAsync(account);

        public async Task<bool> RestoreEmailToFolderAsync(ArchivedEmail email, MailAccount targetAccount, string folderName)
            => await _restorer.RestoreEmailToFolderAsync(email, targetAccount, folderName, false);

        public async Task<bool> RestoreEmailToFolderAsync(ArchivedEmail email, MailAccount targetAccount, string folderName, bool preserveFolderStructure)
            => await _restorer.RestoreEmailToFolderAsync(email, targetAccount, folderName, preserveFolderStructure);

        // ========================================
        // IProviderEmailService (ID-based wrappers)
        // ========================================

        async Task<List<string>> IProviderEmailService.GetMailFoldersAsync(int accountId)
        {
            var account = await _context.MailAccounts.FindAsync(accountId);
            if (account == null)
                return new List<string>();
            return await _folderService.GetMailFoldersAsync(account);
        }

        async Task<bool> IProviderEmailService.RestoreEmailToFolderAsync(int emailId, int targetAccountId, string folderName)
        {
            return await ((IProviderEmailService)this).RestoreEmailToFolderAsync(emailId, targetAccountId, folderName, false);
        }

        async Task<bool> IProviderEmailService.RestoreEmailToFolderAsync(int emailId, int targetAccountId, string folderName, bool preserveFolderStructure)
        {
            var email = await _context.ArchivedEmails
                .Include(e => e.Attachments)
                .FirstOrDefaultAsync(e => e.Id == emailId);

            if (email == null)
                return false;

            var targetAccount = await _context.MailAccounts.FindAsync(targetAccountId);
            if (targetAccount == null)
                return false;

            return await _restorer.RestoreEmailToFolderAsync(email, targetAccount, folderName, preserveFolderStructure);
        }

        Task<(int Successful, int Failed)> IProviderEmailService.RestoreMultipleEmailsWithProgressAsync(
            List<int> emailIds,
            int targetAccountId,
            string folderName,
            Action<int, int, int> progressCallback,
            CancellationToken cancellationToken)
        {
            return ((IProviderEmailService)this).RestoreMultipleEmailsWithProgressAsync(
                emailIds, targetAccountId, folderName, false, progressCallback, cancellationToken);
        }

        async Task<(int Successful, int Failed)> IProviderEmailService.RestoreMultipleEmailsWithProgressAsync(
            List<int> emailIds,
            int targetAccountId,
            string folderName,
            bool preserveFolderStructure,
            Action<int, int, int> progressCallback,
            CancellationToken cancellationToken)
        {
            return await _restorer.RestoreMultipleEmailsWithProgressAsync(
                emailIds, targetAccountId, folderName, preserveFolderStructure, progressCallback, cancellationToken);
        }

        async Task<bool> IProviderEmailService.ResyncAccountAsync(int accountId)
        {
            return await _syncService.ResyncAccountAsync(accountId);
        }

        Task<int> IProviderEmailService.GetEmailCountByAccountAsync(int accountId)
        {
            return _coreService.GetEmailCountByAccountAsync(accountId);
        }
    }
}
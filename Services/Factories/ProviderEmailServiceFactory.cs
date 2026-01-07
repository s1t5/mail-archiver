using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Providers;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Services.Factories
{
    /// <summary>
    /// Factory for creating the appropriate email service based on provider type
    /// Routes operations to IMAP, M365 (Graph), or Import services
    /// </summary>
    public class ProviderEmailServiceFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly MailArchiverDbContext _context;
        private readonly ILogger<ProviderEmailServiceFactory> _logger;

        public ProviderEmailServiceFactory(
            IServiceProvider serviceProvider,
            MailArchiverDbContext context,
            ILogger<ProviderEmailServiceFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Gets the appropriate provider service based on provider type
        /// </summary>
        /// <param name="providerType">The provider type (IMAP, M365, IMPORT)</param>
        /// <returns>Provider-specific email service</returns>
        /// <exception cref="NotSupportedException">Thrown when provider type is not supported</exception>
        public IProviderEmailService GetService(ProviderType providerType)
        {
            _logger.LogDebug("Getting service for provider type: {ProviderType}", providerType);

            return providerType switch
            {
                ProviderType.IMAP => _serviceProvider.GetRequiredService<ImapEmailService>(),
                ProviderType.M365 => _serviceProvider.GetRequiredService<IGraphEmailService>() as IProviderEmailService 
                    ?? throw new InvalidOperationException("GraphEmailService does not implement IProviderEmailService"),
                ProviderType.IMPORT => _serviceProvider.GetRequiredService<ImportEmailService>(),
                _ => throw new NotSupportedException($"Provider type '{providerType}' is not supported")
            };
        }

        /// <summary>
        /// Gets the appropriate provider service for a specific mail account
        /// </summary>
        /// <param name="accountId">The mail account ID</param>
        /// <returns>Provider-specific email service</returns>
        /// <exception cref="InvalidOperationException">Thrown when account is not found</exception>
        public IProviderEmailService GetServiceForAccount(int accountId)
        {
            var account = _context.MailAccounts.Find(accountId);
            if (account == null)
            {
                _logger.LogError("Account with ID {AccountId} not found", accountId);
                throw new InvalidOperationException($"Mail account with ID {accountId} not found");
            }

            _logger.LogDebug("Getting service for account {AccountId} ({AccountName}) with provider {Provider}",
                accountId, account.Name, account.Provider);

            return GetService(account.Provider);
        }

        /// <summary>
        /// Gets the appropriate provider service for a specific mail account (async)
        /// </summary>
        /// <param name="accountId">The mail account ID</param>
        /// <returns>Provider-specific email service</returns>
        /// <exception cref="InvalidOperationException">Thrown when account is not found</exception>
        public async Task<IProviderEmailService> GetServiceForAccountAsync(int accountId)
        {
            var account = await _context.MailAccounts.FindAsync(accountId);
            if (account == null)
            {
                _logger.LogError("Account with ID {AccountId} not found", accountId);
                throw new InvalidOperationException($"Mail account with ID {accountId} not found");
            }

            _logger.LogDebug("Getting service for account {AccountId} ({AccountName}) with provider {Provider}",
                accountId, account.Name, account.Provider);

            return GetService(account.Provider);
        }

        /// <summary>
        /// Gets the appropriate provider service for a mail account object
        /// </summary>
        /// <param name="account">The mail account</param>
        /// <returns>Provider-specific email service</returns>
        /// <exception cref="ArgumentNullException">Thrown when account is null</exception>
        public IProviderEmailService GetServiceForAccount(MailAccount account)
        {
            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            _logger.LogDebug("Getting service for account {AccountName} with provider {Provider}",
                account.Name, account.Provider);

            return GetService(account.Provider);
        }
    }
}

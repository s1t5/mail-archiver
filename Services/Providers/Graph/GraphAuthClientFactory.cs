using Azure.Identity;
using MailArchiver.Models;
using Microsoft.Graph;

namespace MailArchiver.Services.Providers.Graph
{
    /// <summary>
    /// Factory that creates authenticated GraphServiceClient instances for M365 accounts.
    /// Uses client credentials flow with Azure.Identity for automatic token acquisition and refresh.
    /// </summary>
    public class GraphAuthClientFactory
    {
        private readonly ILogger<GraphAuthClientFactory> _logger;

        public GraphAuthClientFactory(ILogger<GraphAuthClientFactory> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Validates that the account has the required credentials for app-only authentication.
        /// </summary>
        public void ValidateAccountCredentials(MailAccount account)
        {
            if (string.IsNullOrEmpty(account.ClientId) || string.IsNullOrEmpty(account.ClientSecret))
            {
                throw new InvalidOperationException(
                    $"M365 account '{account.Name}' requires ClientId and ClientSecret for OAuth authentication");
            }

            // App-only flows (client credentials) require a concrete tenant ID; "common" is only valid
            // for delegated multi-tenant flows and would yield AADSTS9002313 here.
            if (string.IsNullOrWhiteSpace(account.TenantId))
            {
                throw new InvalidOperationException(
                    $"M365 account '{account.Name}' requires a TenantId for application-permission OAuth (client credentials flow).");
            }
        }

        /// <summary>
        /// Creates a GraphServiceClient for the specified M365 account using client credentials flow
        /// with automatic token refresh via Azure.Identity.
        /// </summary>
        /// <param name="account">The M365 mail account</param>
        /// <returns>Configured GraphServiceClient</returns>
        public GraphServiceClient CreateGraphClient(MailAccount account)
        {
            ValidateAccountCredentials(account);

            // Azure.Identity handles token acquisition + refresh automatically.
            var credential = new ClientSecretCredential(
                tenantId: account.TenantId,
                clientId: account.ClientId,
                clientSecret: account.ClientSecret);

            var graphServiceClient = new GraphServiceClient(
                credential,
                new[] { "https://graph.microsoft.com/.default" });

            _logger.LogDebug("Created GraphServiceClient for account '{AccountName}' (tenant: {TenantId})",
                account.Name, account.TenantId);

            return graphServiceClient;
        }
    }
}
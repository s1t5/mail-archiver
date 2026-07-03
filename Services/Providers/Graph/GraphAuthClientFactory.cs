using Azure.Identity;
using MailArchiver.Models;
using Microsoft.Graph;
using GraphUser = Microsoft.Graph.Models.User;

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
        /// Creates a GraphServiceClient directly from tenant credentials.
        /// </summary>
        public GraphServiceClient CreateGraphClient(string clientId, string clientSecret, string tenantId)
        {
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new InvalidOperationException("ClientId and ClientSecret are required for OAuth authentication.");
            }

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new InvalidOperationException("TenantId is required for application-permission OAuth (client credentials flow).");
            }

            var credential = new ClientSecretCredential(
                tenantId: tenantId,
                clientId: clientId,
                clientSecret: clientSecret);

            return new GraphServiceClient(
                credential,
                new[] { "https://graph.microsoft.com/.default" });
        }

        /// <summary>
        /// Lists tenant users that can be represented as mail accounts.
        /// </summary>
        public async Task<List<GraphUser>> GetTenantMailboxUsersAsync(string clientId, string clientSecret, string tenantId, bool includeDisabled = false)
        {
            var graphClient = CreateGraphClient(clientId, clientSecret, tenantId);
            var users = new List<GraphUser>();

            var response = await graphClient.Users.GetAsync(requestConfiguration =>
            {
                requestConfiguration.QueryParameters.Select = new[]
                {
                    "id",
                    "displayName",
                    "mail",
                    "userPrincipalName",
                    "accountEnabled",
                    "userType",
                    "assignedPlans"
                };

                if (!includeDisabled)
                {
                    requestConfiguration.QueryParameters.Filter = "accountEnabled eq true";
                }

                requestConfiguration.QueryParameters.Top = 999;
            });

            while (response != null)
            {
                if (response.Value != null)
                {
                    users.AddRange(response.Value.Where(user =>
                        (!string.IsNullOrWhiteSpace(user.Mail) || !string.IsNullOrWhiteSpace(user.UserPrincipalName))
                        && !IsGuestUser(user)
                        && HasExchangeLicense(user)));
                }

                if (string.IsNullOrWhiteSpace(response.OdataNextLink))
                {
                    break;
                }

                response = await graphClient.Users.WithUrl(response.OdataNextLink).GetAsync();
            }

            _logger.LogInformation("Found {Count} tenant users with mail addresses or UPNs (include disabled: {IncludeDisabled})", users.Count, includeDisabled);
            return users;
        }

        /// <summary>
        /// Returns true if the user is a guest account (userType == "Guest").
        /// </summary>
        private static bool IsGuestUser(GraphUser user)
        {
            return string.Equals(user.UserType, "Guest", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks whether the user has an active Exchange Online service plan.
        /// Defensive: if assignedPlans is null/empty (e.g. due to permission limits),
        /// the user is not excluded to avoid hiding valid accounts.
        /// </summary>
        private static bool HasExchangeLicense(GraphUser user)
        {
            if (user.AssignedPlans == null || user.AssignedPlans.Count == 0)
            {
                return true;
            }

            return user.AssignedPlans.Any(plan =>
                string.Equals(plan.Service, "exchange", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(plan.CapabilityStatus, "Deleted", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(plan.CapabilityStatus, "Suspended", StringComparison.OrdinalIgnoreCase));
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
using Azure.Identity;
using MailArchiver.Models;
using Microsoft.Graph;
using Microsoft.Graph.Authentication;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using GraphUser = Microsoft.Graph.Models.User;

namespace MailArchiver.Services.Providers.Graph
{
    /// <summary>
    /// Factory that creates authenticated GraphServiceClient instances for M365 accounts.
    /// Uses client credentials flow with Azure.Identity for automatic token acquisition and refresh.
    /// Clients are cached per credential set (tenantId + clientId + secret hash) because
    /// GraphServiceClient owns an internal HttpClient that would otherwise leak handlers/sockets
    /// when a new client is created for every sync run. GraphServiceClient and
    /// ClientSecretCredential are thread-safe and intended to be reused.
    /// </summary>
    public class GraphAuthClientFactory
    {
        private readonly ILogger<GraphAuthClientFactory> _logger;
        private readonly ILoggerFactory _loggerFactory;

        // MEMORY FIX: Cache GraphServiceClient instances per credential set. Creating a new
        // GraphServiceClient per operation leaks the internally-owned HttpClient/handler chain
        // (never disposed) and re-acquires OAuth tokens on every sync (each ClientSecretCredential
        // has its own MSAL token cache). Credential changes produce a new cache key automatically.
        private readonly ConcurrentDictionary<string, GraphServiceClient> _clientCache = new();

        public GraphAuthClientFactory(ILogger<GraphAuthClientFactory> logger, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Builds a cache key from tenant credentials. The client secret is hashed so it is
        /// never kept in plain text as a dictionary key.
        /// </summary>
        private static string BuildCacheKey(string clientId, string clientSecret, string tenantId)
        {
            var secretHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(clientSecret)));
            return $"{tenantId}|{clientId}|{secretHash}";
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
        /// Creates (or returns a cached) GraphServiceClient directly from tenant credentials.
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

            return _clientCache.GetOrAdd(BuildCacheKey(clientId, clientSecret, tenantId), _ =>
            {
                var credential = new ClientSecretCredential(
                    tenantId: tenantId,
                    clientId: clientId,
                    clientSecret: clientSecret);

                _logger.LogDebug("Creating new cached GraphServiceClient for tenant {TenantId}", tenantId);

                // The JSON parse node factory sanitizes response payloads with invalid
                // UTF-8 sequences (corrupted Exchange message bodies) before Kiota
                // deserializes them - without it, a single corrupted email fails the
                // entire message page with "The JSON value could not be converted to
                // System.String". See SanitizingJsonParseNodeFactory for details.
                var authProvider = new AzureIdentityAuthenticationProvider(
                    credential,
                    allowedHosts: null,
                    observabilityOptions: null,
                    isCaeEnabled: true,
                    scopes: new[] { "https://graph.microsoft.com/.default" });

                var requestAdapter = new BaseGraphRequestAdapter(
                    authProvider,
                    graphClientOptions: null,
                    parseNodeFactory: new SanitizingJsonParseNodeFactory(
                        _loggerFactory.CreateLogger<SanitizingJsonParseNodeFactory>()),
                    serializationWriterFactory: null,
                    httpClient: null);

                return new GraphServiceClient(requestAdapter);
            });
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
        /// Creates (or returns a cached) GraphServiceClient for the specified M365 account using
        /// client credentials flow with automatic token refresh via Azure.Identity.
        /// </summary>
        /// <param name="account">The M365 mail account</param>
        /// <returns>Configured GraphServiceClient</returns>
        public GraphServiceClient CreateGraphClient(MailAccount account)
        {
            ValidateAccountCredentials(account);

            // Azure.Identity handles token acquisition + refresh automatically.
            var graphServiceClient = CreateGraphClient(account.ClientId!, account.ClientSecret!, account.TenantId!);

            _logger.LogDebug("Resolved GraphServiceClient for account '{AccountName}' (tenant: {TenantId})",
                account.Name, account.TenantId);

            return graphServiceClient;
        }
    }
}
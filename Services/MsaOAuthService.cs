using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;

namespace MailArchiver.Services
{
    public interface IMsaOAuthService
    {
        string BuildAuthorizationUrl(string clientId, string redirectUri, string state);
        Task<MsaTokenResult> ExchangeCodeAsync(string code, string clientId, string clientSecret, string redirectUri);
        Task<MsaTokenResult> RefreshAccessTokenAsync(string refreshToken, string clientId, string clientSecret);
    }

    public class MsaTokenResult
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime Expiry { get; set; }
    }

    public class MsaOAuthService : IMsaOAuthService
    {
        private static readonly string Authority = "https://login.microsoftonline.com/common/oauth2/v2.0";
        private static readonly string[] Scopes = ["IMAP.AccessAsUser.All", "offline_access", "openid"];

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MsaOAuthService> _logger;

        public MsaOAuthService(IHttpClientFactory httpClientFactory, ILogger<MsaOAuthService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public string BuildAuthorizationUrl(string clientId, string redirectUri, string state)
        {
            var scopeString = string.Join(" ", Scopes);
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["client_id"] = clientId;
            query["response_type"] = "code";
            query["redirect_uri"] = redirectUri;
            query["scope"] = scopeString;
            query["state"] = state;
            query["response_mode"] = "query";
            query["prompt"] = "select_account";
            return $"{Authority}/authorize?{query}";
        }

        public async Task<MsaTokenResult> ExchangeCodeAsync(string code, string clientId, string clientSecret, string redirectUri)
        {
            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["scope"] = string.Join(" ", Scopes),
            };
            return await PostTokenAsync(body);
        }

        public async Task<MsaTokenResult> RefreshAccessTokenAsync(string refreshToken, string clientId, string clientSecret)
        {
            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = refreshToken,
                ["scope"] = string.Join(" ", Scopes),
            };
            return await PostTokenAsync(body);
        }

        private async Task<MsaTokenResult> PostTokenAsync(Dictionary<string, string> body)
        {
            var client = _httpClientFactory.CreateClient("MsaOAuth");
            var response = await client.PostAsync($"{Authority}/token", new FormUrlEncodedContent(body));
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("MSA token request failed ({Status}): {Body}", response.StatusCode, json);
                throw new InvalidOperationException($"MSA token request failed: {response.StatusCode} — {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var accessToken = root.GetProperty("access_token").GetString()!;
            var expiresIn = root.GetProperty("expires_in").GetInt32();
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? string.Empty : string.Empty;

            return new MsaTokenResult
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Expiry = DateTime.UtcNow.AddSeconds(expiresIn - 60), // 60s buffer
            };
        }
    }
}

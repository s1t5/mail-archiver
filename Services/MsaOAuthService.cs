using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace MailArchiver.Services
{
    public interface IMsaOAuthService
    {
        // Device Code Flow (recommended — no public URL required)
        Task<DeviceCodeResult> StartDeviceCodeAsync(string clientId);
        Task<MsaTokenResult?> PollDeviceCodeAsync(string clientId, string deviceCode);

        // PKCE Authorization Code Flow (requires public redirect URI)
        string BuildAuthorizationUrl(string clientId, string redirectUri, string state, out string codeVerifier);
        Task<MsaTokenResult> ExchangeCodeAsync(string code, string clientId, string? clientSecret, string redirectUri, string codeVerifier);

        // Token refresh (used by both flows)
        Task<MsaTokenResult> RefreshAccessTokenAsync(string refreshToken, string clientId, string? clientSecret);
    }

    public class DeviceCodeResult
    {
        public string DeviceCode { get; set; } = string.Empty;
        public string UserCode { get; set; } = string.Empty;
        public string VerificationUri { get; set; } = string.Empty;
        public int ExpiresIn { get; set; }
        public int Interval { get; set; }
    }

    public class MsaTokenResult
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime Expiry { get; set; }
    }

    public class MsaOAuthService : IMsaOAuthService
    {
        private static readonly string Authority = "https://login.microsoftonline.com/consumers/oauth2/v2.0";
        private static readonly string[] Scopes = ["https://outlook.office.com/IMAP.AccessAsUser.All", "offline_access", "openid"];

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MsaOAuthService> _logger;

        public MsaOAuthService(IHttpClientFactory httpClientFactory, ILogger<MsaOAuthService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<DeviceCodeResult> StartDeviceCodeAsync(string clientId)
        {
            var client = _httpClientFactory.CreateClient("MsaOAuth");
            var body = new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["scope"] = string.Join(" ", Scopes),
            };
            var response = await client.PostAsync($"{Authority}/devicecode", new FormUrlEncodedContent(body));
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("MSA device code request failed ({Status}): {Body}", response.StatusCode, json);
                throw new InvalidOperationException($"Failed to start device code flow: {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new DeviceCodeResult
            {
                DeviceCode = root.GetProperty("device_code").GetString()!,
                UserCode = root.GetProperty("user_code").GetString()!,
                VerificationUri = root.GetProperty("verification_uri").GetString()!,
                ExpiresIn = root.GetProperty("expires_in").GetInt32(),
                Interval = root.TryGetProperty("interval", out var iv) ? iv.GetInt32() : 5,
            };
        }

        // Returns null when authorization is still pending, throws on expiry/denial, returns token on success.
        public async Task<MsaTokenResult?> PollDeviceCodeAsync(string clientId, string deviceCode)
        {
            var client = _httpClientFactory.CreateClient("MsaOAuth");
            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["client_id"] = clientId,
                ["device_code"] = deviceCode,
            };
            var response = await client.PostAsync($"{Authority}/token", new FormUrlEncodedContent(body));
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!response.IsSuccessStatusCode)
            {
                var error = root.TryGetProperty("error", out var e) ? e.GetString() : "unknown";
                if (error == "authorization_pending" || error == "slow_down")
                    return null; // still waiting

                var desc = root.TryGetProperty("error_description", out var d) ? d.GetString() : json;
                _logger.LogWarning("MSA device code poll error: {Error} — {Desc}", error, desc);
                throw new InvalidOperationException(error == "expired_token"
                    ? "Der Code ist abgelaufen. Bitte erneut autorisieren."
                    : $"Authorization failed: {desc}");
            }

            var accessToken = root.GetProperty("access_token").GetString()!;
            var expiresIn = root.GetProperty("expires_in").GetInt32();
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? string.Empty : string.Empty;
            return new MsaTokenResult
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Expiry = DateTime.UtcNow.AddSeconds(expiresIn - 60),
            };
        }

        public string BuildAuthorizationUrl(string clientId, string redirectUri, string state, out string codeVerifier)
        {
            codeVerifier = GenerateCodeVerifier();
            var codeChallenge = ComputeCodeChallenge(codeVerifier);
            var query = HttpUtility.ParseQueryString(string.Empty);
            query["client_id"] = clientId;
            query["response_type"] = "code";
            query["redirect_uri"] = redirectUri;
            query["scope"] = string.Join(" ", Scopes);
            query["state"] = state;
            query["response_mode"] = "query";
            query["prompt"] = "select_account";
            query["code_challenge"] = codeChallenge;
            query["code_challenge_method"] = "S256";
            return $"{Authority}/authorize?{query}";
        }

        public async Task<MsaTokenResult> ExchangeCodeAsync(string code, string clientId, string? clientSecret, string redirectUri, string codeVerifier)
        {
            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["scope"] = string.Join(" ", Scopes),
                ["code_verifier"] = codeVerifier,
            };
            if (!string.IsNullOrEmpty(clientSecret))
                body["client_secret"] = clientSecret;
            return await PostTokenAsync(body);
        }

        public async Task<MsaTokenResult> RefreshAccessTokenAsync(string refreshToken, string clientId, string? clientSecret)
        {
            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken,
                ["scope"] = string.Join(" ", Scopes),
            };
            if (!string.IsNullOrEmpty(clientSecret))
                body["client_secret"] = clientSecret;
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
                Expiry = DateTime.UtcNow.AddSeconds(expiresIn - 60),
            };
        }

        private static string GenerateCodeVerifier()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Base64UrlEncode(bytes);
        }

        private static string ComputeCodeChallenge(string codeVerifier)
        {
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
            return Base64UrlEncode(hash);
        }

        private static string Base64UrlEncode(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

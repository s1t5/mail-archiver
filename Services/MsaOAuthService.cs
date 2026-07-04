using System.Text.Json;
using MailArchiver.Models;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services
{
    public interface IMsaOAuthService
    {
        Task<DeviceCodeResult> StartDeviceCodeAsync(string? clientId);
        Task<MsaPollResult> PollDeviceCodeAsync(string? clientId, string deviceCode, int currentInterval);
        Task<MsaTokenResult> RefreshAccessTokenAsync(string refreshToken, string? clientId, string? clientSecret);
        /// <summary>
        /// Returns the configured default ClientId (or null when none is configured).
        /// </summary>
        string? GetDefaultClientId();
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

    public enum MsaPollStatus { Pending, SlowDown, Success }

    public class MsaPollResult
    {
        public MsaPollStatus Status { get; set; }
        public MsaTokenResult? Token { get; set; }
        public int IntervalSeconds { get; set; }
    }

    // Terminal OAuth errors (expired_token, access_denied, invalid_grant, etc.).
    // Transient network/HTTP failures are NOT terminal — they propagate as their original exception.
    public class MsaDeviceCodeTerminalException : Exception
    {
        public string ErrorCode { get; }
        public MsaDeviceCodeTerminalException(string errorCode, string message) : base(message)
            => ErrorCode = errorCode;
    }

    public class MsaOAuthService : IMsaOAuthService
    {
        private static readonly string[] Scopes = ["https://outlook.office.com/IMAP.AccessAsUser.All", "offline_access"];

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MsaOAuthService> _logger;
        private readonly MsaOAuthOptions _options;

        public MsaOAuthService(
            IHttpClientFactory httpClientFactory,
            ILogger<MsaOAuthService> logger,
            IOptions<MsaOAuthOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _options = options.Value;
        }

        private string Authority => string.IsNullOrWhiteSpace(_options.Authority)
            ? "https://login.microsoftonline.com/common/oauth2/v2.0"
            : _options.Authority;

        public string? GetDefaultClientId()
            => _options.HasDefaultClientId ? _options.DefaultClientId : null;

        // Resolves the ClientId to use: per-account override wins, otherwise the configured default.
        // Throws when neither is available — callers should surface this to the user/operator.
        private string ResolveClientId(string? clientId)
        {
            if (!string.IsNullOrWhiteSpace(clientId))
                return clientId;
            if (_options.HasDefaultClientId)
                return _options.DefaultClientId;
            throw new InvalidOperationException(
                "No MSA ClientId configured. Either set 'MsaOAuth:DefaultClientId' in appsettings.json " +
                "or enter a per-account ClientId in the account form.");
        }

        public async Task<DeviceCodeResult> StartDeviceCodeAsync(string? clientId)
        {
            var resolvedClientId = ResolveClientId(clientId);
            var client = _httpClientFactory.CreateClient("MsaOAuth");
            var body = new Dictionary<string, string>
            {
                ["client_id"] = resolvedClientId,
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

        // Returns MsaPollResult (Pending/SlowDown/Success). Throws MsaDeviceCodeTerminalException on
        // terminal OAuth errors (expired_token, access_denied, invalid_grant, ...). Transient
        // network/HTTP/parse failures propagate as their original exception so the caller can retry.
        // RFC 8628 §3.5: on "slow_down" the polling interval MUST increase by 5 seconds.
        public async Task<MsaPollResult> PollDeviceCodeAsync(string? clientId, string deviceCode, int currentInterval)
        {
            var resolvedClientId = ResolveClientId(clientId);
            var client = _httpClientFactory.CreateClient("MsaOAuth");
            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["client_id"] = resolvedClientId,
                ["device_code"] = deviceCode,
            };
            var response = await client.PostAsync($"{Authority}/token", new FormUrlEncodedContent(body));
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!response.IsSuccessStatusCode)
            {
                var error = root.TryGetProperty("error", out var e) ? e.GetString() ?? "unknown" : "unknown";
                if (error == "authorization_pending")
                    return new MsaPollResult { Status = MsaPollStatus.Pending, IntervalSeconds = currentInterval };
                if (error == "slow_down")
                {
                    var newInterval = currentInterval + 5;
                    return new MsaPollResult { Status = MsaPollStatus.SlowDown, IntervalSeconds = newInterval };
                }

                var desc = root.TryGetProperty("error_description", out var d) ? d.GetString() : json;
                _logger.LogWarning("MSA device code poll terminal error: {Error} — {Desc}", error, desc);
                throw new MsaDeviceCodeTerminalException(error,
                    error == "expired_token"
                        ? "Der Code ist abgelaufen. Bitte erneut autorisieren."
                        : $"Authorization failed: {desc}");
            }

            var accessToken = root.GetProperty("access_token").GetString()!;
            var expiresIn = root.GetProperty("expires_in").GetInt32();
            var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? string.Empty : string.Empty;
            return new MsaPollResult
            {
                Status = MsaPollStatus.Success,
                IntervalSeconds = currentInterval,
                Token = new MsaTokenResult
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    Expiry = DateTime.UtcNow.AddSeconds(expiresIn - 60),
                },
            };
        }

        public async Task<MsaTokenResult> RefreshAccessTokenAsync(string refreshToken, string? clientId, string? clientSecret)
        {
            var resolvedClientId = ResolveClientId(clientId);
            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = resolvedClientId,
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

    }
}

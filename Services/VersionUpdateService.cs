using System.Reflection;
using System.Text.Json;
using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services
{
    /// <summary>
    /// Fetches release notes from GitHub once per application lifetime and
    /// checks if the current admin user has already seen them.
    /// Since no new releases appear while the app is running, the GitHub API
    /// is called at most once — on the first admin login after startup.
    /// </summary>
    public class VersionUpdateService : IVersionUpdateService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ReleaseNotesOptions _options;
        private readonly ILogger<VersionUpdateService> _logger;

        // Thread-safe, one-time lazy initialisation
        private volatile string? _cachedReleaseNotes;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _initialized;

        public VersionUpdateService(
            IHttpClientFactory httpClientFactory,
            IServiceScopeFactory scopeFactory,
            IOptions<ReleaseNotesOptions> options,
            ILogger<VersionUpdateService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<ReleaseNotesResult> GetReleaseNotesForCurrentVersionAsync(int userId)
        {
            if (!_options.Enabled)
                return new ReleaseNotesResult { ShouldShow = false };

            var version = ResolveAppVersion();

            // Fetch release notes from cache or GitHub
            var body = await GetOrFetchReleaseNotesAsync(version);

            if (string.IsNullOrWhiteSpace(body))
                return new ReleaseNotesResult { Version = version, ShouldShow = false };

            // Check if the user has already dismissed this version
            var hasSeen = await HasUserSeenVersionAsync(userId, version);

            return new ReleaseNotesResult
            {
                Version = version,
                Body = body,
                ShouldShow = !hasSeen
            };
        }

        /// <inheritdoc />
        public async Task DismissVersionAsync(int userId)
        {
            var version = ResolveAppVersion();

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

            var user = await db.Users.FindAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("DismissVersion: User {UserId} not found", userId);
                return;
            }

            user.LastSeenChangelogVersion = version;
            await db.SaveChangesAsync();

            _logger.LogInformation("User {UserId} dismissed changelog for version {Version}", userId, version);
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private string ResolveAppVersion()
        {
            // Prefer configured override, otherwise read from assembly
            if (!string.IsNullOrWhiteSpace(_options.AppVersion))
                return _options.AppVersion;

            var asm = Assembly.GetExecutingAssembly();
            var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return attr?.InformationalVersion ?? asm.GetName().Version?.ToString() ?? "0.0.0";
        }

        private async Task<string?> GetOrFetchReleaseNotesAsync(string version)
        {
            // Double-check locking: fetch once per application lifetime
            if (_initialized)
                return _cachedReleaseNotes;

            await _initLock.WaitAsync();
            try
            {
                if (_initialized)
                    return _cachedReleaseNotes;

                try
                {
                    _cachedReleaseNotes = await FetchReleaseNotesFromGitHubAsync(version);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch release notes from GitHub for version {Version}", version);
                    _cachedReleaseNotes = null;
                }

                _initialized = true;
                return _cachedReleaseNotes;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task<string?> FetchReleaseNotesFromGitHubAsync(string version)
        {
            // Hardcoded — release notes are always fetched from the public repository
            const string owner = "s1t5";
            const string repo = "mail-archiver";
            var url = $"https://api.github.com/repos/{owner}/{repo}/releases";

            var client = _httpClientFactory.CreateClient("GitHubReleases");
            client.Timeout = TimeSpan.FromSeconds(_options.RequestTimeoutSeconds);
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            client.DefaultRequestHeaders.Add("User-Agent", "MailArchiver-Enterprise");

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var releases = JsonSerializer.Deserialize<List<GitHubRelease>>(json);

            if (releases == null || releases.Count == 0)
                return null;

            // Try exact tag match first (e.g. "v2606.2"), then fallback to prefix match
            var match = releases.FirstOrDefault(r =>
                r.TagName?.Equals(version, StringComparison.OrdinalIgnoreCase) == true ||
                r.TagName?.StartsWith(version, StringComparison.OrdinalIgnoreCase) == true);

            return match?.Body;
        }

        private async Task<bool> HasUserSeenVersionAsync(int userId, string version)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

            var user = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.LastSeenChangelogVersion)
                .FirstOrDefaultAsync();

            return user != null && user.Equals(version, StringComparison.OrdinalIgnoreCase);
        }

        // -----------------------------------------------------------------------
        // JSON models for GitHub API deserialization
        // -----------------------------------------------------------------------

        private class GitHubRelease
        {
            [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
            public string? TagName { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("body")]
            public string? Body { get; set; }
        }
    }
}
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MailArchiver.Data;
using MailArchiver.Models;
using MailArchiver.Services.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services
{
    /// <summary>
    /// Fetches the GitHub release list at most once per cache interval and serves two
    /// consumers from it: the release notes for the installed version and the update
    /// hint that tells administrators a newer stable release exists.
    /// </summary>
    public class VersionUpdateService : IVersionUpdateService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ReleaseNotesOptions _options;
        private readonly ILogger<VersionUpdateService> _logger;

        // The release list changes rarely, so it is cached for a configurable interval
        // instead of once per application lifetime: a container that runs for months
        // would otherwise never notice a new release.
        private IReadOnlyList<GitHubRelease>? _cachedReleases;
        private DateTime _cacheFetchedUtc;
        private readonly SemaphoreSlim _fetchLock = new(1, 1);

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
            var releases = await GetReleasesAsync();
            var body = FindReleaseForVersion(releases, version)?.Body;

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
        public async Task<UpdateStatusResult> GetUpdateStatusAsync()
        {
            // The update hint needs a configured version. Without one the footer shows
            // "dev" and the assembly falls back to 1.0.0, which would rank below every
            // release and produce a permanent false hint.
            var currentVersion = _options.AppVersion?.Trim();

            if (!_options.Enabled || !_options.UpdateHintEnabled || string.IsNullOrWhiteSpace(currentVersion))
                return new UpdateStatusResult { CurrentVersion = currentVersion, UpdateAvailable = false };

            var releases = await GetReleasesAsync();
            var latest = releases
                .Where(r => !r.Draft && !r.Prerelease && !string.IsNullOrWhiteSpace(r.TagName))
                .OrderByDescending(r => r.PublishedAt ?? DateTimeOffset.MinValue)
                .FirstOrDefault();

            if (latest?.TagName == null)
                return new UpdateStatusResult { CurrentVersion = currentVersion, UpdateAvailable = false };

            if (!AppVersionComparer.TryCompare(currentVersion, latest.TagName, out var comparison))
            {
                _logger.LogDebug(
                    "Update check skipped: cannot compare installed version {Current} with release tag {Latest}",
                    currentVersion, latest.TagName);
                return new UpdateStatusResult { CurrentVersion = currentVersion, UpdateAvailable = false };
            }

            return new UpdateStatusResult
            {
                CurrentVersion = currentVersion,
                LatestVersion = latest.TagName,
                UpdateAvailable = comparison < 0,
                ReleaseUrl = latest.HtmlUrl
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

        private TimeSpan CacheLifetime => TimeSpan.FromHours(Math.Max(1, _options.UpdateCheckIntervalHours));

        private string ResolveAppVersion()
        {
            // Prefer configured override, otherwise read from assembly
            if (!string.IsNullOrWhiteSpace(_options.AppVersion))
                return _options.AppVersion;

            var asm = Assembly.GetExecutingAssembly();
            var attr = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return attr?.InformationalVersion ?? asm.GetName().Version?.ToString() ?? "0.0.0";
        }

        private static GitHubRelease? FindReleaseForVersion(IReadOnlyList<GitHubRelease> releases, string version)
        {
            // Exact tag first (e.g. "2611.2"), then fall back to a prefix match so a
            // version like "2611.2+abc123" still finds its release.
            return releases.FirstOrDefault(r =>
                       r.TagName?.Equals(version, StringComparison.OrdinalIgnoreCase) == true)
                   ?? releases.FirstOrDefault(r =>
                       r.TagName?.StartsWith(version, StringComparison.OrdinalIgnoreCase) == true);
        }

        private async Task<IReadOnlyList<GitHubRelease>> GetReleasesAsync()
        {
            var cached = _cachedReleases;
            if (cached != null && DateTime.UtcNow - _cacheFetchedUtc < CacheLifetime)
                return cached;

            await _fetchLock.WaitAsync();
            try
            {
                cached = _cachedReleases;
                if (cached != null && DateTime.UtcNow - _cacheFetchedUtc < CacheLifetime)
                    return cached;

                try
                {
                    _cachedReleases = await FetchReleasesFromGitHubAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch releases from GitHub");
                    // A stale list is still better than none: keep it and retry after the
                    // interval instead of calling GitHub on every page load.
                }

                _cacheFetchedUtc = DateTime.UtcNow;
                return _cachedReleases ?? Array.Empty<GitHubRelease>();
            }
            finally
            {
                _fetchLock.Release();
            }
        }

        private async Task<IReadOnlyList<GitHubRelease>> FetchReleasesFromGitHubAsync()
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
            return JsonSerializer.Deserialize<List<GitHubRelease>>(json) ?? new List<GitHubRelease>();
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
            [JsonPropertyName("tag_name")]
            public string? TagName { get; set; }

            [JsonPropertyName("body")]
            public string? Body { get; set; }

            [JsonPropertyName("draft")]
            public bool Draft { get; set; }

            [JsonPropertyName("prerelease")]
            public bool Prerelease { get; set; }

            [JsonPropertyName("published_at")]
            public DateTimeOffset? PublishedAt { get; set; }

            [JsonPropertyName("html_url")]
            public string? HtmlUrl { get; set; }
        }
    }
}

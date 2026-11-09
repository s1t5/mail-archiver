using System.Net;
using System.Text;
using System.Text.Json;
using MailArchiver.Models;
using MailArchiver.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MailArchiver.Tests.Services;

/// <summary>
/// The update hint must only fire for a stable release that is newer than the installed
/// version, and it must not call GitHub on every page load. These tests drive the service
/// through a stubbed HTTP handler; the database is deliberately unavailable because the
/// update check must not depend on it.
/// </summary>
public class VersionUpdateServiceTests
{
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return respond(request);
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class UnusedScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new NotSupportedException("The update check must not need the database.");
    }

    private static VersionUpdateService CreateService(
        HttpMessageHandler handler,
        Action<ReleaseNotesOptions>? configure = null)
    {
        var options = new ReleaseNotesOptions
        {
            Enabled = true,
            AppVersion = "2611.2",
            RequestTimeoutSeconds = 10,
            UpdateHintEnabled = true,
            UpdateCheckIntervalHours = 6
        };

        configure?.Invoke(options);

        return new VersionUpdateService(
            new Factory(handler),
            new UnusedScopeFactory(),
            Options.Create(options),
            NullLogger<VersionUpdateService>.Instance);
    }

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static string ReleasesJson(params (string Tag, string PublishedAt, bool Draft, bool Prerelease)[] releases) =>
        JsonSerializer.Serialize(releases.Select(r => new
        {
            tag_name = r.Tag,
            body = $"notes for {r.Tag}",
            draft = r.Draft,
            prerelease = r.Prerelease,
            published_at = r.PublishedAt,
            html_url = $"https://example.test/{r.Tag}"
        }));

    [Fact]
    public async Task NewerStableRelease_ReportsUpdate()
    {
        var handler = new Handler(_ => Task.FromResult(Ok(ReleasesJson(
            ("2611.2", "2026-10-01T10:00:00Z", false, false),
            ("2612.0", "2026-11-01T10:00:00Z", false, false)))));

        var status = await CreateService(handler).GetUpdateStatusAsync();

        Assert.True(status.UpdateAvailable);
        Assert.Equal("2611.2", status.CurrentVersion);
        Assert.Equal("2612.0", status.LatestVersion);
        Assert.Equal("https://example.test/2612.0", status.ReleaseUrl);
    }

    [Fact]
    public async Task SameVersion_ReportsNoUpdate()
    {
        var handler = new Handler(_ => Task.FromResult(Ok(ReleasesJson(
            ("2611.2", "2026-10-01T10:00:00Z", false, false)))));

        var status = await CreateService(handler).GetUpdateStatusAsync();

        Assert.False(status.UpdateAvailable);
        Assert.Equal("2611.2", status.LatestVersion);
    }

    [Fact]
    public async Task InstalledVersionNewerThanLatestRelease_ReportsNoUpdate()
    {
        var handler = new Handler(_ => Task.FromResult(Ok(ReleasesJson(
            ("2611.2", "2026-10-01T10:00:00Z", false, false)))));

        var status = await CreateService(handler, o => o.AppVersion = "2612.0").GetUpdateStatusAsync();

        Assert.False(status.UpdateAvailable);
    }

    [Fact]
    public async Task DraftsAndPrereleases_AreIgnored()
    {
        // 2700.0 is a draft, 2699.0 a prerelease, 2612.0 the newest stable release.
        var handler = new Handler(_ => Task.FromResult(Ok(ReleasesJson(
            ("2700.0", "2026-11-05T10:00:00Z", true, false),
            ("2699.0", "2026-11-04T10:00:00Z", false, true),
            ("2612.0", "2026-11-01T10:00:00Z", false, false)))));

        var status = await CreateService(handler).GetUpdateStatusAsync();

        Assert.True(status.UpdateAvailable);
        Assert.Equal("2612.0", status.LatestVersion);
    }

    [Fact]
    public async Task NewestStableRelease_IsChosenByPublishDate_NotByListOrder()
    {
        var handler = new Handler(_ => Task.FromResult(Ok(ReleasesJson(
            ("2611.2", "2026-10-01T10:00:00Z", false, false),
            ("2612.0", "2026-11-01T10:00:00Z", false, false)))));

        var status = await CreateService(handler).GetUpdateStatusAsync();

        Assert.Equal("2612.0", status.LatestVersion);
    }

    [Fact]
    public async Task NonNumericReleaseTag_ProducesNoHint()
    {
        var handler = new Handler(_ => Task.FromResult(Ok(ReleasesJson(
            ("nightly", "2026-11-01T10:00:00Z", false, false)))));

        var status = await CreateService(handler).GetUpdateStatusAsync();

        Assert.False(status.UpdateAvailable);
    }

    [Theory]
    [InlineData(false, true, "2611.2")]
    [InlineData(true, false, "2611.2")]
    [InlineData(true, true, "")]
    [InlineData(true, true, "   ")]
    public async Task DisabledOrUnversioned_DoesNotCallGitHub(bool enabled, bool hintEnabled, string appVersion)
    {
        var handler = new Handler(_ => throw new InvalidOperationException("GitHub must not be called."));
        var service = CreateService(handler, o =>
        {
            o.Enabled = enabled;
            o.UpdateHintEnabled = hintEnabled;
            o.AppVersion = appVersion;
        });

        var status = await service.GetUpdateStatusAsync();

        Assert.False(status.UpdateAvailable);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task FailedFetch_ReturnsNoUpdateInsteadOfThrowing()
    {
        var handler = new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

        var status = await CreateService(handler).GetUpdateStatusAsync();

        Assert.False(status.UpdateAvailable);
    }

    [Fact]
    public async Task ReleaseList_IsFetchedOncePerCacheInterval()
    {
        var handler = new Handler(_ => Task.FromResult(Ok(ReleasesJson(
            ("2612.0", "2026-11-01T10:00:00Z", false, false)))));
        var service = CreateService(handler);

        await service.GetUpdateStatusAsync();
        await service.GetUpdateStatusAsync();

        Assert.Equal(1, handler.Calls);
    }
}

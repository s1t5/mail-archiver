using MailArchiver.Controllers.Api.V1;
using MailArchiver.Models;
using MailArchiver.Services.Monitoring;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MailArchiver.Tests.Monitoring;

public class MonitoringApiControllerTests
{
    private static HealthCheckService HealthService(
        params (string Name, Func<HealthCheckResult> Check, string[] Tags)[] checks)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var builder = services.AddHealthChecks();
        foreach (var (name, check, tags) in checks)
        {
            builder.AddCheck(name, check, tags);
        }

        return services.BuildServiceProvider().GetRequiredService<HealthCheckService>();
    }

    private static MonitoringApiController Create(
        HealthCheckService health,
        MetricsOptions? options = null,
        MetricsStore? store = null,
        IMetricsRecorder? recorder = null) =>
        new(
            health,
            store ?? new MetricsStore(),
            recorder ?? new MetricsRecorder(),
            Options.Create(options ?? new MetricsOptions { Enabled = true }))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    [Fact]
    public void Live_Returns200WithoutChecks()
    {
        var controller = Create(HealthService());

        var result = Assert.IsType<OkObjectResult>(controller.Live());

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
    }

    [Fact]
    public async Task Ready_Unhealthy_Returns503()
    {
        var controller = Create(HealthService(
            ("database", () => HealthCheckResult.Unhealthy("down"), new[] { "ready" })));

        var result = Assert.IsType<ObjectResult>(await controller.Ready());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
    }

    [Fact]
    public async Task Ready_Healthy_Returns200()
    {
        var controller = Create(HealthService(
            ("database", () => HealthCheckResult.Healthy("up"), new[] { "ready" })));

        var result = Assert.IsType<ObjectResult>(await controller.Ready());

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
    }

    [Fact]
    public async Task Health_Degraded_Returns200()
    {
        var controller = Create(HealthService(
            ("sync", () => HealthCheckResult.Degraded("stale"), new[] { "sync" })));

        var result = Assert.IsType<ObjectResult>(await controller.Health());

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
    }

    [Fact]
    public void Metrics_Disabled_Returns404()
    {
        var controller = Create(HealthService(), new MetricsOptions { Enabled = false });

        Assert.IsType<NotFoundResult>(controller.Metrics());
    }

    [Fact]
    public void Metrics_Enabled_WritesPrometheusText()
    {
        var store = new MetricsStore();
        store.Set(new MetricsSnapshot(
            3, 1, 0, 2, 1, true,
            new[] { new AccountSyncSnapshot(1, "Archive", DateTime.UtcNow, true) },
            DateTime.UtcNow));

        var controller = Create(HealthService(), store: store);

        var result = Assert.IsType<ContentResult>(controller.Metrics());

        Assert.Equal("text/plain; version=0.0.4; charset=utf-8", result.ContentType);
        Assert.Contains("mailarchiver_emails 3", result.Content);
        Assert.Contains("mailarchiver_raw_messages_missing 1", result.Content);
    }
}

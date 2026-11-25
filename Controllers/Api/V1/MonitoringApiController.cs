using MailArchiver.Attributes;
using MailArchiver.Models;
using MailArchiver.Services.Monitoring;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MailArchiver.Controllers.Api.V1;

/// <summary>
/// Health and metrics for external monitoring. Every action requires an admin API key; a
/// missing or invalid key is rejected by the API key middleware with 401, a valid non-admin
/// key by <see cref="AdminOnlyAttribute"/> with 403.
/// </summary>
[Route("api/v1")]
[AdminOnly]
public class MonitoringApiController : ApiControllerBase
{
    private readonly HealthCheckService _healthChecks;
    private readonly MetricsStore _metricsStore;
    private readonly IMetricsRecorder _metricsRecorder;
    private readonly MetricsOptions _metricsOptions;

    public MonitoringApiController(
        HealthCheckService healthChecks,
        MetricsStore metricsStore,
        IMetricsRecorder metricsRecorder,
        IOptions<MetricsOptions> metricsOptions)
    {
        _healthChecks = healthChecks;
        _metricsStore = metricsStore;
        _metricsRecorder = metricsRecorder;
        _metricsOptions = metricsOptions.Value;
    }

    /// <summary>Liveness: the process accepts requests. No dependency is touched.</summary>
    [HttpGet("health/live")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Live() => Ok(new { status = "Healthy" });

    /// <summary>Readiness: PostgreSQL is reachable and the archive schema exists.</summary>
    [HttpGet("health/ready")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ready()
    {
        var report = await _healthChecks.CheckHealthAsync(
            registration => registration.Tags.Contains("ready"),
            HttpContext.RequestAborted);

        return HealthResponse(report);
    }

    /// <summary>Aggregate health, including sync freshness. Degraded stays HTTP 200.</summary>
    [HttpGet("health")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Health()
    {
        var report = await _healthChecks.CheckHealthAsync(HttpContext.RequestAborted);
        return HealthResponse(report);
    }

    /// <summary>Prometheus text exposition. Disabled unless Metrics:Enabled is true.</summary>
    [HttpGet("metrics")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Produces("text/plain")]
    public IActionResult Metrics()
    {
        if (!_metricsOptions.Enabled)
        {
            return NotFound();
        }

        var text = MetricsTextWriter.Write(
            _metricsStore.Current,
            _metricsStore.DatabaseReachable,
            _metricsRecorder.Snapshot());

        return Content(text, "text/plain; version=0.0.4; charset=utf-8");
    }

    private IActionResult HealthResponse(HealthReport report)
    {
        var statusCode = report.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK;

        return StatusCode(statusCode, new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Status.ToString())
        });
    }
}

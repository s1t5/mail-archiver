using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services.Monitoring;

/// <summary>
/// Degrades the aggregate health report when an enabled account has not synced within the
/// configured window. It is not part of readiness: an old sync means stale mail, not a
/// process that cannot serve requests.
/// </summary>
public sealed class SyncFreshnessHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HealthOptions _options;

    public SyncFreshnessHealthCheck(
        IServiceScopeFactory scopeFactory,
        IOptions<HealthOptions> options)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_options.SyncStaleMinutes <= 0)
        {
            return HealthCheckResult.Healthy("Sync freshness check is disabled.");
        }

        var cutoff = DateTime.UtcNow.AddMinutes(-_options.SyncStaleMinutes);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

        var stale = await dbContext.MailAccounts
            .CountAsync(a => a.IsEnabled && a.LastSync < cutoff, cancellationToken);

        return stale == 0
            ? HealthCheckResult.Healthy(
                $"All enabled accounts synced within {_options.SyncStaleMinutes} minutes.")
            : HealthCheckResult.Degraded(
                $"{stale} enabled account(s) have not synced within {_options.SyncStaleMinutes} minutes.");
    }
}

using MailArchiver.Data;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailArchiver.Services.Monitoring;

/// <summary>
/// Rebuilds the metrics snapshot on a fixed interval. The scrape endpoint only reads the
/// stored snapshot, so a Prometheus poll cannot turn into a table scan.
/// </summary>
public sealed class MetricsRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MetricsStore _store;
    private readonly MetricsOptions _options;
    private readonly RawMessageOptions _rawMessageOptions;
    private readonly ILogger<MetricsRefreshService> _logger;

    public MetricsRefreshService(
        IServiceScopeFactory scopeFactory,
        MetricsStore store,
        IOptions<MetricsOptions> options,
        IOptions<RawMessageOptions> rawMessageOptions,
        ILogger<MetricsRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _store = store;
        _options = options.Value;
        _rawMessageOptions = rawMessageOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        // Do not compete with migrations and the first sync at startup.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(Math.Max(1, _options.RefreshMinutes)));

        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshOnceAsync(stoppingToken);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task RefreshOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MailArchiverDbContext>();

            var snapshot = await BuildSnapshotAsync(
                dbContext, _rawMessageOptions.Enabled, cancellationToken);

            _store.Set(snapshot);
            _logger.LogDebug(
                "Refreshed monitoring metrics: {Emails} emails, {Missing} without original.",
                snapshot.Emails, snapshot.MissingOriginals);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown, not a database problem.
        }
        catch (Exception ex)
        {
            _store.MarkUnreachable();
            _logger.LogError(ex, "Could not refresh the monitoring metrics snapshot.");
        }
    }

    internal static async Task<MetricsSnapshot> BuildSnapshotAsync(
        MailArchiverDbContext dbContext,
        bool rawMessagesEnabled,
        CancellationToken cancellationToken)
    {
        var emails = await dbContext.ArchivedEmails.LongCountAsync(cancellationToken);
        var enabledAccounts = await dbContext.MailAccounts
            .CountAsync(a => a.IsEnabled, cancellationToken);
        var disabledAccounts = await dbContext.MailAccounts
            .CountAsync(a => !a.IsEnabled, cancellationToken);
        var rawMessages = await dbContext.RawMessages.LongCountAsync(cancellationToken);
        var missingOriginals = await dbContext.ArchivedEmails
            .Where(e => !dbContext.RawMessages.Any(r => r.ArchivedEmailId == e.Id))
            .LongCountAsync(cancellationToken);
        var accounts = await dbContext.MailAccounts
            .OrderBy(a => a.Id)
            .Select(a => new AccountSyncSnapshot(a.Id, a.Name, a.LastSync, a.IsEnabled))
            .ToListAsync(cancellationToken);

        return new MetricsSnapshot(
            emails,
            enabledAccounts,
            disabledAccounts,
            rawMessages,
            missingOriginals,
            rawMessagesEnabled,
            accounts,
            DateTime.UtcNow);
    }
}

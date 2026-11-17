namespace MailArchiver.Services.Monitoring;

/// <summary>
/// Last known sync state of one mail account, captured for the per-account metrics.
/// </summary>
public sealed record AccountSyncSnapshot(int Id, string Name, DateTime LastSync, bool IsEnabled);

/// <summary>
/// Point-in-time counts read from the archive. Built by <see cref="MetricsRefreshService"/>
/// and served by the metrics endpoint; never computed during a scrape.
/// </summary>
public sealed record MetricsSnapshot(
    long Emails,
    int EnabledAccounts,
    int DisabledAccounts,
    long RawMessages,
    long MissingOriginals,
    bool RawMessagesEnabled,
    IReadOnlyList<AccountSyncSnapshot> Accounts,
    DateTime RefreshedAtUtc);

/// <summary>
/// Process-local counters for events that cannot be derived from the database. They reset
/// on restart, which is expected for Prometheus counters and handled by rate()/increase().
/// </summary>
public sealed record MetricsCounterSnapshot(
    long RawMessageSize,
    long RawMessageWrite,
    long RawMessageNoCapture,
    long SyncFailed,
    long SyncRateLimited,
    long SyncTimeout);

/// <summary>
/// Canonical reason labels, shared by the call sites and the text writer so a typo cannot
/// silently create a new time series.
/// </summary>
public static class MetricsReasons
{
    public const string Size = "size";
    public const string Write = "write";
    public const string NoCapture = "no_capture";
    public const string Failed = "failed";
    public const string RateLimited = "rate_limited";
    public const string Timeout = "timeout";
}

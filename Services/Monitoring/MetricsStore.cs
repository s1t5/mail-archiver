namespace MailArchiver.Services.Monitoring;

/// <summary>
/// Holds the last successful snapshot for the metrics endpoint. A failed refresh keeps the
/// previous counts but flips <see cref="DatabaseReachable"/> so an alert can tell a stale
/// snapshot from a healthy one.
/// </summary>
public sealed class MetricsStore
{
    private MetricsSnapshot? _current;
    private bool _databaseReachable;

    public MetricsSnapshot? Current => Volatile.Read(ref _current);

    public bool DatabaseReachable => Volatile.Read(ref _databaseReachable);

    public void Set(MetricsSnapshot snapshot)
    {
        Volatile.Write(ref _current, snapshot);
        Volatile.Write(ref _databaseReachable, true);
    }

    public void MarkUnreachable()
    {
        Volatile.Write(ref _databaseReachable, false);
    }
}

namespace MailArchiver.Services.Monitoring;

/// <summary>
/// Process-local Prometheus counters. Registered as a singleton so every sync and archive
/// path increments the same series. No external package: the text format is written by
/// <see cref="MetricsTextWriter"/>.
/// </summary>
public sealed class MetricsRecorder : IMetricsRecorder
{
    private long _rawMessageSize;
    private long _rawMessageWrite;
    private long _rawMessageNoCapture;
    private long _syncFailed;
    private long _syncRateLimited;
    private long _syncTimeout;

    public void RawMessageNotStored(string reason)
    {
        switch (reason)
        {
            case MetricsReasons.Size:
                Interlocked.Increment(ref _rawMessageSize);
                break;
            case MetricsReasons.Write:
                Interlocked.Increment(ref _rawMessageWrite);
                break;
            case MetricsReasons.NoCapture:
            default:
                Interlocked.Increment(ref _rawMessageNoCapture);
                break;
        }
    }

    public void SyncFailed(string reason)
    {
        switch (reason)
        {
            case MetricsReasons.RateLimited:
                Interlocked.Increment(ref _syncRateLimited);
                break;
            case MetricsReasons.Timeout:
                Interlocked.Increment(ref _syncTimeout);
                break;
            case MetricsReasons.Failed:
            default:
                Interlocked.Increment(ref _syncFailed);
                break;
        }
    }

    public MetricsCounterSnapshot Snapshot() => new(
        Interlocked.Read(ref _rawMessageSize),
        Interlocked.Read(ref _rawMessageWrite),
        Interlocked.Read(ref _rawMessageNoCapture),
        Interlocked.Read(ref _syncFailed),
        Interlocked.Read(ref _syncRateLimited),
        Interlocked.Read(ref _syncTimeout));
}

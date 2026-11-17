namespace MailArchiver.Services.Monitoring;

/// <summary>
/// Records events that are not derivable from the database. Implementations must be safe
/// for concurrent calls; the archive path is used by multiple sync jobs.
/// </summary>
public interface IMetricsRecorder
{
    /// <summary>An original message could not be stored. Reason is a <see cref="MetricsReasons"/> value.</summary>
    void RawMessageNotStored(string reason);

    /// <summary>A sync job ended in failure, rate limiting or timeout. Reason is a <see cref="MetricsReasons"/> value.</summary>
    void SyncFailed(string reason);

    /// <summary>Current counter values for the metrics endpoint.</summary>
    MetricsCounterSnapshot Snapshot();
}

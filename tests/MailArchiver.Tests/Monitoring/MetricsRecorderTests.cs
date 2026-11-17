using MailArchiver.Services.Monitoring;

namespace MailArchiver.Tests.Monitoring;

public class MetricsRecorderTests
{
    [Fact]
    public void CountersAreSeparatedByReason()
    {
        var recorder = new MetricsRecorder();

        recorder.RawMessageNotStored(MetricsReasons.Size);
        recorder.RawMessageNotStored(MetricsReasons.Write);
        recorder.RawMessageNotStored(MetricsReasons.NoCapture);
        recorder.RawMessageNotStored(MetricsReasons.NoCapture);
        recorder.SyncFailed(MetricsReasons.Failed);
        recorder.SyncFailed(MetricsReasons.RateLimited);
        recorder.SyncFailed(MetricsReasons.Timeout);

        var snapshot = recorder.Snapshot();

        Assert.Equal(1, snapshot.RawMessageSize);
        Assert.Equal(1, snapshot.RawMessageWrite);
        Assert.Equal(2, snapshot.RawMessageNoCapture);
        Assert.Equal(1, snapshot.SyncFailed);
        Assert.Equal(1, snapshot.SyncRateLimited);
        Assert.Equal(1, snapshot.SyncTimeout);
    }

    [Fact]
    public void UnknownReasonFallsBackToGenericBucket()
    {
        var recorder = new MetricsRecorder();

        recorder.RawMessageNotStored("other");
        recorder.SyncFailed("other");

        var snapshot = recorder.Snapshot();

        Assert.Equal(1, snapshot.RawMessageNoCapture);
        Assert.Equal(1, snapshot.SyncFailed);
    }

    [Fact]
    public void EarlierSnapshotIsNotChangedByLaterIncrements()
    {
        var recorder = new MetricsRecorder();

        recorder.RawMessageNotStored(MetricsReasons.Size);
        var first = recorder.Snapshot();

        recorder.RawMessageNotStored(MetricsReasons.Size);
        var second = recorder.Snapshot();

        Assert.Equal(1, first.RawMessageSize);
        Assert.Equal(2, second.RawMessageSize);
    }
}

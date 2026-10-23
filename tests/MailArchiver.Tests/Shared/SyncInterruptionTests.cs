using MailArchiver.Models;
using MailArchiver.Services.Shared;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// Two independent signals can stop a running sync: somebody cancels the job from the UI, or the
/// account's sync timeout elapses. Before, only the first one existed as far as the sync loops were
/// concerned — the timeout lived in a CancellationToken nothing ever consulted, so
/// MailSync:TimeoutMinutes had no effect whatsoever.
///
/// Both now meet in one place, which is worth pinning down for two reasons. The loops must not drift
/// apart from each other or between the two providers, and the reasons must stay distinguishable:
/// a cancel ends the job as failed, a timeout ends it as a pause that keeps its checkpoints. Getting
/// that backwards would either lose a user's explicit cancel or silently discard resumable progress.
/// </summary>
public class SyncInterruptionTests
{
    // ---- nothing to do -----------------------------------------------------------------------

    [Fact]
    public void A_running_job_without_a_timeout_keeps_going()
    {
        Assert.Equal(SyncStopReason.None, SyncInterruption.Evaluate(SyncJobStatus.Running, false));
    }

    [Fact]
    public void No_job_and_no_timeout_keeps_going()
    {
        Assert.Equal(SyncStopReason.None, SyncInterruption.Evaluate(null, false));
    }

    [Theory]
    [InlineData(SyncJobStatus.Running)]
    [InlineData(SyncJobStatus.Completed)]
    [InlineData(SyncJobStatus.Failed)]
    [InlineData(SyncJobStatus.RateLimited)]
    [InlineData(SyncJobStatus.TimedOut)]
    public void Only_the_cancelled_status_stops_a_sync(SyncJobStatus status)
    {
        Assert.Equal(SyncStopReason.None, SyncInterruption.Evaluate(status, false));
    }

    // ---- the two reasons ---------------------------------------------------------------------

    [Fact]
    public void A_cancelled_job_stops_the_sync()
    {
        Assert.Equal(SyncStopReason.Cancelled, SyncInterruption.Evaluate(SyncJobStatus.Cancelled, false));
    }

    [Fact]
    public void An_elapsed_timeout_stops_the_sync()
    {
        Assert.Equal(SyncStopReason.TimedOut, SyncInterruption.Evaluate(SyncJobStatus.Running, true));
    }

    [Fact]
    public void The_timeout_applies_to_a_sync_that_runs_without_a_job()
    {
        // A sync started outside the job machinery has no status to inspect, but the timeout still
        // has to be able to stop it — otherwise it would run unbounded.
        Assert.Equal(SyncStopReason.TimedOut, SyncInterruption.Evaluate(null, true));
    }

    // ---- precedence --------------------------------------------------------------------------

    [Fact]
    public void A_cancel_outranks_a_timeout_when_both_apply()
    {
        // Somebody deliberately stopped this job. That is the more specific statement about what
        // happened, and it is the one that must reach the job status, because the two are not
        // interchangeable afterwards: a cancel fails the job, a timeout pauses it.
        Assert.Equal(SyncStopReason.Cancelled, SyncInterruption.Evaluate(SyncJobStatus.Cancelled, true));
    }
}

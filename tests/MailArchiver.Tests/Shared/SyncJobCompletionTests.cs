using MailArchiver.Models;
using MailArchiver.Services.Shared;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// Two rules, and the second is the one that is easy to lose. A sync that dies has to have its job
/// closed, or the job sits on Running for the life of the process and everything that reads
/// IsAccountSyncing believes the account is still syncing. But a sync that closed its own job said
/// something more precise than "something threw", and that has to survive.
/// </summary>
public class SyncJobCompletionTests
{
    [Fact]
    public void A_job_still_running_has_to_be_closed_by_the_caller()
    {
        Assert.True(SyncJobCompletion.NeedsClosing(SyncJobStatus.Running));
    }

    [Theory]
    [InlineData(SyncJobStatus.TimedOut)]
    [InlineData(SyncJobStatus.Cancelled)]
    [InlineData(SyncJobStatus.Failed)]
    [InlineData(SyncJobStatus.RateLimited)]
    [InlineData(SyncJobStatus.Completed)]
    public void A_job_that_ended_itself_is_left_alone(SyncJobStatus status)
    {
        // TimedOut is the case with teeth: it means "paused, checkpoints kept, LastSync untouched".
        // Replacing it with Failed would describe a run that lost its resume point instead.
        Assert.False(SyncJobCompletion.NeedsClosing(status));
    }

    [Fact]
    public void No_job_means_nothing_to_close()
    {
        // The sync threw before StartSyncAsync returned, or the job is already gone.
        Assert.False(SyncJobCompletion.NeedsClosing(null));
    }
}

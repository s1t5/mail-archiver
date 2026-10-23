using MailArchiver.Services.Shared;
using Xunit;

namespace MailArchiver.Tests.Shared;

public class SyncCompletionPolicyTests
{
    [Fact]
    public void A_clean_run_may_advance_last_sync()
    {
        Assert.True(SyncCompletionPolicy.MayAdvanceLastSync(failedEmails: 0, failedFolders: 0));
    }

    [Fact]
    public void A_failed_message_still_blocks_last_sync()
    {
        Assert.False(SyncCompletionPolicy.MayAdvanceLastSync(failedEmails: 1, failedFolders: 0));
    }

    // The behaviour this change is careful NOT to alter. Before, a folder that could not be opened
    // was written into FailedEmails as one-per-processed-message, which blocked LastSync as a side
    // effect of a fabricated count. It is now counted as a folder and blocks deliberately. Should
    // the project ever decide that an inaccessible folder must not hold up the whole account, this
    // is the assertion that has to change, and it is the only one.
    [Fact]
    public void A_failed_folder_still_blocks_last_sync()
    {
        Assert.False(SyncCompletionPolicy.MayAdvanceLastSync(failedEmails: 0, failedFolders: 1));
    }

    [Fact]
    public void Both_kinds_of_failure_together_block_last_sync()
    {
        Assert.False(SyncCompletionPolicy.MayAdvanceLastSync(failedEmails: 3, failedFolders: 2));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, false)]
    [InlineData(0, 1, false)]
    [InlineData(500, 0, false)]
    [InlineData(0, 25, false)]
    public void The_rule_is_a_plain_conjunction(int failedEmails, int failedFolders, bool expected)
    {
        Assert.Equal(expected, SyncCompletionPolicy.MayAdvanceLastSync(failedEmails, failedFolders));
    }
}

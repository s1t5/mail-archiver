using MailArchiver.Models;
using MailArchiver.Services.Shared;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// The log is what turns "Failed folders: 16" into something an operator can act on, and the cap is
/// what keeps it from costing more than it is worth: sync jobs live for 24 hours and on a large
/// installation there are thousands of them at once.
///
/// Budgeting per kind rather than overall is the part that has to hold. An account with a thousand
/// unreadable messages would otherwise fill one shared budget before the sixteen folder problems —
/// the ones that explain why the account is stuck — ever got in.
/// </summary>
public class SyncIssueLogTests
{
    private static SyncIssue Issue(SyncIssueKind kind, string folder = "INBOX")
        => new() { Kind = kind, Folder = folder, Reason = "because" };

    [Fact]
    public void An_empty_log_says_so()
    {
        var log = new SyncIssueLog(20);

        Assert.True(log.IsEmpty);
        Assert.Empty(log.Issues);
        Assert.Equal(0, log.CountFor(SyncIssueKind.FolderFailed));
        Assert.Equal(0, log.DroppedFor(SyncIssueKind.FolderFailed));
    }

    [Fact]
    public void Entries_are_kept_in_the_order_they_happened()
    {
        var log = new SyncIssueLog(20);
        log.Add(Issue(SyncIssueKind.FolderFailed, "first"));
        log.Add(Issue(SyncIssueKind.MessageFailed, "second"));

        Assert.Equal(new[] { "first", "second" }, log.Issues.Select(i => i.Folder));
    }

    // ---- the cap, which is the whole point ----------------------------------------------------

    [Fact]
    public void Each_kind_has_its_own_budget()
    {
        // The case this exists for: a flood of one kind must not push out the others.
        var log = new SyncIssueLog(2);

        for (var i = 0; i < 50; i++)
        {
            log.Add(Issue(SyncIssueKind.MessageFailed));
        }
        log.Add(Issue(SyncIssueKind.FolderMissing, "still gets in"));

        Assert.Equal(2, log.CountFor(SyncIssueKind.MessageFailed));
        Assert.Equal(48, log.DroppedFor(SyncIssueKind.MessageFailed));
        Assert.Equal(1, log.CountFor(SyncIssueKind.FolderMissing));
        Assert.Equal(0, log.DroppedFor(SyncIssueKind.FolderMissing));
        Assert.Contains(log.Issues, i => i.Folder == "still gets in");
    }

    [Fact]
    public void What_no_longer_fits_is_counted_rather_than_lost_silently()
    {
        var log = new SyncIssueLog(1);
        log.Add(Issue(SyncIssueKind.FolderFailed));
        log.Add(Issue(SyncIssueKind.FolderFailed));
        log.Add(Issue(SyncIssueKind.FolderFailed));

        Assert.Equal(1, log.CountFor(SyncIssueKind.FolderFailed));
        Assert.Equal(2, log.DroppedFor(SyncIssueKind.FolderFailed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_non_positive_budget_switches_the_log_off(int maxPerKind)
    {
        // The way out if the log ever costs more than it is worth. The counters on the job still
        // work, only the detail stops being collected - and the drop count says it was switched off
        // rather than that nothing happened.
        var log = new SyncIssueLog(maxPerKind);
        log.Add(Issue(SyncIssueKind.FolderFailed));

        Assert.True(log.IsEmpty);
        Assert.Equal(1, log.DroppedFor(SyncIssueKind.FolderFailed));
    }

    [Fact]
    public void Null_is_ignored_rather_than_thrown_at_the_sync()
    {
        // Nothing should be able to abort a sync because a diagnostic entry could not be built.
        var log = new SyncIssueLog(20);
        log.Add(null!);

        Assert.True(log.IsEmpty);
    }

    [Fact]
    public void Concurrent_writers_do_not_corrupt_the_list()
    {
        // Written by the sync task and read by the page at the same time. A List would throw here.
        var log = new SyncIssueLog(1000);

        Parallel.For(0, 500, _ => log.Add(Issue(SyncIssueKind.MessageFailed)));

        Assert.Equal(500, log.CountFor(SyncIssueKind.MessageFailed));
        Assert.Equal(500, log.Issues.Count);
    }
}

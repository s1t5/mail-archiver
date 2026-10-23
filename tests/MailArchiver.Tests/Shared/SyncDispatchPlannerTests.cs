using MailArchiver.Services.Shared;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// The scheduler tick no longer waits for the accounts it starts, which puts two rules in front of
/// it that the old batch shape never needed: an account already running must not be started again,
/// and when more accounts are due than there are free slots, the order decides who waits.
///
/// Both are the kind of rule that looks obviously right and goes wrong quietly. A missing running
/// check starts a second sync on a mailbox already being read; a stable-but-arbitrary order lets one
/// account be passed over tick after tick while later-due accounts keep taking the slots. Hence the
/// tests.
/// </summary>
public class SyncDispatchPlannerTests
{
    private static readonly DateTime Now = new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<int> Select(
        (int, DateTime)[] candidates,
        params int[] running)
        => SyncDispatchPlanner.SelectDueAccounts(candidates, Now, running.ToHashSet());

    // ---- due or not --------------------------------------------------------------------------

    [Fact]
    public void An_account_due_in_the_future_is_not_dispatched()
    {
        Assert.Empty(Select(new[] { (1, Now.AddMinutes(1)) }));
    }

    [Fact]
    public void An_account_due_exactly_now_is_dispatched()
    {
        Assert.Equal(new[] { 1 }, Select(new[] { (1, Now) }));
    }

    [Fact]
    public void Nothing_due_dispatches_nothing()
    {
        Assert.Empty(Select(Array.Empty<(int, DateTime)>()));
    }

    // ---- the running guard -------------------------------------------------------------------

    [Fact]
    public void An_account_already_running_is_skipped_even_though_it_is_overdue()
    {
        // Its due time stays in the past on purpose — that is what puts it at the front once it
        // finishes — so the guard has to be the running set, not the clock.
        Assert.Empty(Select(new[] { (1, Now.AddHours(-5)) }, running: 1));
    }

    [Fact]
    public void The_others_still_go_while_one_account_runs()
    {
        Assert.Equal(new[] { 2 }, Select(new[] { (1, Now.AddHours(-5)), (2, Now.AddMinutes(-1)) }, running: 1));
    }

    // ---- order -------------------------------------------------------------------------------

    [Fact]
    public void The_most_overdue_account_goes_first()
    {
        var order = Select(new[]
        {
            (1, Now.AddMinutes(-1)),
            (2, Now.AddHours(-6)),
            (3, Now.AddMinutes(-30)),
        });

        Assert.Equal(new[] { 2, 3, 1 }, order);
    }

    [Fact]
    public void Enumeration_order_does_not_decide_who_wins()
    {
        // Same set, handed over the other way round. A scheduler that just took them as they came
        // would starve whichever account happened to sort late.
        var order = Select(new[]
        {
            (3, Now.AddMinutes(-30)),
            (1, Now.AddMinutes(-1)),
            (2, Now.AddHours(-6)),
        });

        Assert.Equal(new[] { 2, 3, 1 }, order);
    }

    [Fact]
    public void Accounts_due_at_the_same_moment_come_out_in_a_stable_order()
    {
        // Ties are broken by id so a run is reproducible; without it the order would depend on
        // dictionary enumeration and the same backlog could behave differently twice.
        var order = Select(new[] { (7, Now), (2, Now), (5, Now) });

        Assert.Equal(new[] { 2, 5, 7 }, order);
    }
}

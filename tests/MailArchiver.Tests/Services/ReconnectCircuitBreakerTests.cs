using MailArchiver.Services.Providers.Imap;

namespace MailArchiver.Tests.Services;

public class ReconnectCircuitBreakerTests
{
    [Fact]
    public void RecordFailure_3Times_ShouldAbort_returns_true()
    {
        var cb = new ReconnectCircuitBreaker(maxConsecutiveFailures: 3);

        Assert.False(cb.RecordFailure());
        Assert.False(cb.RecordFailure());
        Assert.True(cb.RecordFailure());
        Assert.True(cb.ShouldAbort);
        Assert.Equal(3, cb.ConsecutiveFailures);
    }

    [Fact]
    public void RecordFailure_2Times_then_RecordSuccess_resets_counter()
    {
        var cb = new ReconnectCircuitBreaker(maxConsecutiveFailures: 3);

        cb.RecordFailure();
        cb.RecordFailure();
        Assert.Equal(2, cb.ConsecutiveFailures);

        cb.RecordSuccess();

        Assert.Equal(0, cb.ConsecutiveFailures);
        Assert.False(cb.ShouldAbort);
    }

    [Fact]
    public void RecordFailure_then_RecordParseError_does_not_increment_reconnect_counter()
    {
        var cb = new ReconnectCircuitBreaker(maxConsecutiveFailures: 3);

        cb.RecordFailure();
        Assert.Equal(1, cb.ConsecutiveFailures);

        cb.RecordParseError();

        Assert.Equal(1, cb.ConsecutiveFailures);
        Assert.True(cb.SkipNextReconnectGate);
    }

    [Fact]
    public void RecordParseError_sets_skip_flag_once_then_ConsumeSkipGate_resets()
    {
        var cb = new ReconnectCircuitBreaker(maxConsecutiveFailures: 3);

        Assert.False(cb.SkipNextReconnectGate);

        cb.RecordParseError();
        Assert.True(cb.SkipNextReconnectGate);

        cb.ConsumeSkipGate();
        Assert.False(cb.SkipNextReconnectGate);
    }

    [Fact]
    public void RecordParseError_does_not_set_ShouldAbort()
    {
        var cb = new ReconnectCircuitBreaker(maxConsecutiveFailures: 3);

        cb.RecordParseError();
        cb.RecordParseError();
        cb.RecordParseError();
        cb.RecordParseError();

        Assert.False(cb.ShouldAbort);
        Assert.Equal(0, cb.ConsecutiveFailures);
    }

    [Fact]
    public void RecordFailure_after_RecordParseError_still_counts_toward_abort()
    {
        var cb = new ReconnectCircuitBreaker(maxConsecutiveFailures: 3);

        cb.RecordParseError();
        cb.ConsumeSkipGate();

        // Now a real reconnect failure happens
        cb.RecordFailure();
        cb.RecordFailure();
        var shouldAbort = cb.RecordFailure();

        Assert.True(shouldAbort);
        Assert.Equal(3, cb.ConsecutiveFailures);
    }

    [Fact]
    public void Fresh_instance_has_zero_state()
    {
        var cb = new ReconnectCircuitBreaker(maxConsecutiveFailures: 3);

        Assert.Equal(0, cb.ConsecutiveFailures);
        Assert.False(cb.SkipNextReconnectGate);
        Assert.False(cb.ShouldAbort);
    }

    [Fact]
    public void RecordSuccess_resets_counter_after_abort_threshold_reached()
    {
        var cb = new ReconnectCircuitBreaker(maxConsecutiveFailures: 3);

        cb.RecordFailure();
        cb.RecordFailure();
        cb.RecordFailure();
        Assert.True(cb.ShouldAbort);

        cb.RecordSuccess();

        Assert.False(cb.ShouldAbort);
        Assert.Equal(0, cb.ConsecutiveFailures);
    }

    [Fact]
    public void RecordFailure_with_threshold_1_aborts_on_first_failure()
    {
        var cb = new ReconnectCircuitBreaker(maxConsecutiveFailures: 1);

        var shouldAbort = cb.RecordFailure();

        Assert.True(shouldAbort);
        Assert.Equal(1, cb.ConsecutiveFailures);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void RecordFailure_with_higher_threshold_does_not_abort_on_first_failure(int threshold)
    {
        var cb = new ReconnectCircuitBreaker(threshold);

        var shouldAbort = cb.RecordFailure();

        Assert.False(shouldAbort);
        Assert.Equal(1, cb.ConsecutiveFailures);
    }

    [Fact]
    public void ConsumeSkipGate_without_prior_RecordParseError_is_noop()
    {
        var cb = new ReconnectCircuitBreaker(maxConsecutiveFailures: 3);

        cb.ConsumeSkipGate();

        Assert.False(cb.SkipNextReconnectGate);
    }
}
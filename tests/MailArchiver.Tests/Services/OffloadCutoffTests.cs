using MailArchiver.Services.Shared;

namespace MailArchiver.Tests.Services;

/// <summary>
/// A cutoff is resolved to an absolute date once, when the job is created, and the window is
/// inclusive at both ends of a day. Mail close to midnight is where getting this wrong shows up.
/// </summary>
public class OffloadCutoffTests
{
    private static readonly DateTime Now = new(2026, 8, 21, 14, 37, 52);

    [Fact]
    public void FromRelativeMonths_ResolvesToTheStartOfTheDay()
    {
        var cutoff = OffloadCutoff.FromRelativeMonths(Now, 6);
        Assert.Equal(new DateTime(2026, 2, 21, 0, 0, 0), cutoff);
        Assert.Equal(TimeSpan.Zero, cutoff.TimeOfDay);
    }

    [Fact]
    public void FromRelativeMonths_TwelveMonths_IsTheSameDayAYearEarlier()
    {
        Assert.Equal(new DateTime(2025, 8, 21), OffloadCutoff.FromRelativeMonths(Now, 12));
    }

    [Fact]
    public void FromRelativeMonths_ClampsShortMonths()
    {
        // 31 March minus one month is 28 February, not an invalid date.
        var march31 = new DateTime(2026, 3, 31, 9, 0, 0);
        Assert.Equal(new DateTime(2026, 2, 28), OffloadCutoff.FromRelativeMonths(march31, 1));
    }

    [Fact]
    public void FromRelativeDays_ResolvesToTheStartOfTheDay()
    {
        Assert.Equal(new DateTime(2026, 8, 11), OffloadCutoff.FromRelativeDays(Now, 10));
    }

    [Fact]
    public void FromRelative_NegativeValue_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OffloadCutoff.FromRelativeMonths(Now, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => OffloadCutoff.FromRelativeDays(Now, -1));
    }

    [Fact]
    public void FromAbsolute_DropsTheTimeOfDay()
    {
        Assert.Equal(new DateTime(2025, 8, 1),
            OffloadCutoff.FromAbsolute(new DateTime(2025, 8, 1, 23, 59, 59)));
    }

    // ------------------------------------------------------------------ window edges

    [Fact]
    public void IsInWindow_MidnightOnTheCutoffDay_IsIncluded()
    {
        var cutoff = new DateTime(2026, 2, 21);
        Assert.True(OffloadCutoff.IsInWindow(new DateTime(2026, 2, 21, 0, 0, 0), cutoff, null));
    }

    [Fact]
    public void IsInWindow_OneSecondBeforeTheCutoff_IsExcluded()
    {
        var cutoff = new DateTime(2026, 2, 21);
        Assert.False(OffloadCutoff.IsInWindow(new DateTime(2026, 2, 20, 23, 59, 59), cutoff, null));
    }

    [Fact]
    public void IsInWindow_UpperBoundIncludesTheWholeOfThatDay()
    {
        // Mirrors the search filter: SentDate <= toDate.AddDays(1).AddSeconds(-1).
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 6, 30);

        Assert.True(OffloadCutoff.IsInWindow(new DateTime(2026, 6, 30, 23, 59, 59), from, to));
        Assert.False(OffloadCutoff.IsInWindow(new DateTime(2026, 7, 1, 0, 0, 0), from, to));
    }

    [Fact]
    public void ToInclusiveEnd_IsTheLastSecondOfTheDay()
    {
        Assert.Equal(new DateTime(2026, 6, 30, 23, 59, 59),
            OffloadCutoff.ToInclusiveEnd(new DateTime(2026, 6, 30, 8, 15, 0)));
    }

    [Fact]
    public void IsInWindow_WithoutUpperBound_AcceptsFutureDates()
    {
        var from = new DateTime(2026, 1, 1);
        Assert.True(OffloadCutoff.IsInWindow(new DateTime(2030, 1, 1), from, null));
    }

    // ------------------------------------------------------------------ stability

    [Fact]
    public void ResolvedCutoff_DoesNotDriftWhenTheClockMoves()
    {
        // The reason a relative window is resolved once and stored: a job may run for days and
        // be repeated afterwards, and it has to select the same mail every time.
        var resolved = OffloadCutoff.FromRelativeMonths(Now, 6);
        var laterResolved = OffloadCutoff.FromRelativeMonths(Now.AddDays(3), 6);

        Assert.NotEqual(resolved, laterResolved);
        Assert.Equal(new DateTime(2026, 2, 21), resolved);
    }
}

using MailArchiver.Services.Shared;
using System.Globalization;
using Xunit;

namespace MailArchiver.Tests.Shared;

/// <summary>
/// Unit tests for <see cref="DashboardPeriods"/>: which resolution and window pairs the
/// dashboard offers, and what range a chosen pair resolves to.
/// </summary>
public class DashboardPeriodsTests
{
    private static PeriodWindow Window(string key) =>
        DashboardPeriods.Windows.Single(w => w.Key == key);

    // A fixed instant inside a month, so that a resolved range never depends on when the
    // suite runs.
    private static readonly DateTime _now = new(2026, 9, 18, 8, 42, 17, DateTimeKind.Unspecified);

    [Fact]
    public void Default_IsOfferedAndIsTheHistogramTheDashboardAlwaysShowed()
    {
        Assert.True(DashboardPeriods.IsOffered(
            DashboardPeriods.DefaultGranularity, DashboardPeriods.DefaultWindow));

        var range = DashboardPeriods.Resolve(
            DashboardPeriods.DefaultGranularity, DashboardPeriods.DefaultWindow, _now);

        Assert.Equal(12, range.Buckets.Count);
        Assert.Equal(new DateTime(2025, 10, 1), range.Buckets[0].Start);
        Assert.Equal(new DateTime(2026, 9, 1), range.Buckets[^1].Start);
        Assert.Equal(new DateTime(2026, 10, 1), range.EndExclusive);
    }

    [Fact]
    public void Default_MonthLabelKeepsTheNameAndYearForm()
    {
        var range = DashboardPeriods.Resolve(
            DashboardPeriods.DefaultGranularity, DashboardPeriods.DefaultWindow, _now);

        var expected =
            $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(9)} 2026";
        Assert.Equal(expected, range.Buckets[^1].Label);
    }

    [Fact]
    public void Offered_HoursOnlyReachAsFarAsTheBarsStayReadable()
    {
        Assert.True(DashboardPeriods.IsOffered(PeriodGranularity.Hour, Window("7d")));
        Assert.False(DashboardPeriods.IsOffered(PeriodGranularity.Hour, Window("14d")));
    }

    [Fact]
    public void Offered_APairThatWouldDrawASingleBarIsNotOffered()
    {
        // One bar is a number, not a distribution: a day of day-wide buckets, or a month of
        // month-wide ones, say nothing that the counter cards do not already say.
        Assert.False(DashboardPeriods.IsOffered(PeriodGranularity.Day, Window("1d")));
        Assert.False(DashboardPeriods.IsOffered(PeriodGranularity.Month, Window("1m")));
        Assert.False(DashboardPeriods.IsOffered(PeriodGranularity.Year, Window("1y")));
    }

    [Fact]
    public void Offered_ReachingBackAsFarAsTheArchiveGoesIsWholeYearsOnly()
    {
        var all = Window("all");
        Assert.True(DashboardPeriods.IsOffered(PeriodGranularity.Year, all));
        Assert.False(DashboardPeriods.IsOffered(PeriodGranularity.Month, all));
        Assert.False(DashboardPeriods.IsOffered(PeriodGranularity.Day, all));
        Assert.False(DashboardPeriods.IsOffered(PeriodGranularity.Hour, all));
    }

    [Fact]
    public void Offered_EveryGranularityHasSomethingToOffer()
    {
        foreach (var granularity in DashboardPeriods.Granularities)
            Assert.NotEmpty(DashboardPeriods.OfferedWindows(granularity));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("nonsense", "nonsense")]
    [InlineData("Decade", "42q")]
    public void Canonicalize_JunkFallsBackToTheDefault(string? granularity, string? window)
    {
        var (resolvedGranularity, resolvedWindow) = DashboardPeriods.Canonicalize(granularity, window);

        Assert.Equal(DashboardPeriods.DefaultGranularity, resolvedGranularity);
        Assert.Equal(DashboardPeriods.DefaultWindowKey, resolvedWindow.Key);
    }

    [Fact]
    public void Canonicalize_IsCaseInsensitive()
    {
        var (granularity, window) = DashboardPeriods.Canonicalize("hOuR", "7D");

        Assert.Equal(PeriodGranularity.Hour, granularity);
        Assert.Equal("7d", window.Key);
    }

    [Fact]
    public void Canonicalize_KeepsTheGranularityAndMovesTheWindowToTheNearestOffered()
    {
        // Hours do not reach a year back. The resolution asked for is the coarser choice of the
        // two, so it stays and the window moves to the longest one hours do offer.
        var (granularity, window) = DashboardPeriods.Canonicalize("Hour", "1y");

        Assert.Equal(PeriodGranularity.Hour, granularity);
        Assert.Equal("7d", window.Key);
        Assert.True(DashboardPeriods.IsOffered(granularity, window));
    }

    [Fact]
    public void Canonicalize_AlwaysAnswersWithAnOfferedPair()
    {
        foreach (var granularity in DashboardPeriods.Granularities)
            foreach (var window in DashboardPeriods.Windows)
            {
                var (g, w) = DashboardPeriods.Canonicalize(granularity.ToString(), window.Key);
                Assert.True(DashboardPeriods.IsOffered(g, w),
                    $"{granularity}/{window.Key} resolved to {g}/{w.Key}, which is not offered");
            }
    }

    [Fact]
    public void Resolve_HourBucketsCoverTheWindowAndEndWithTheCurrentHour()
    {
        var range = DashboardPeriods.Resolve(PeriodGranularity.Hour, Window("7d"), _now);

        Assert.Equal(7 * 24, range.Buckets.Count);
        Assert.Equal(new DateTime(2026, 9, 18, 8, 0, 0), range.Buckets[^1].Start);
        Assert.Equal(new DateTime(2026, 9, 18, 9, 0, 0), range.EndExclusive);
        Assert.Equal(new DateTime(2026, 9, 11, 9, 0, 0), range.FilterStart);
    }

    [Fact]
    public void Resolve_DayBucketsStartAtMidnightAndIncludeToday()
    {
        var range = DashboardPeriods.Resolve(PeriodGranularity.Day, Window("28d"), _now);

        Assert.Equal(28, range.Buckets.Count);
        Assert.Equal(new DateTime(2026, 9, 18), range.Buckets[^1].Start);
        Assert.Equal(new DateTime(2026, 9, 19), range.EndExclusive);
    }

    [Fact]
    public void Resolve_AFixedWindowIsBoundedBelowAndCollectsNothingOlder()
    {
        var range = DashboardPeriods.Resolve(PeriodGranularity.Month, Window("2y"), _now);

        Assert.Equal(24, range.Buckets.Count);
        Assert.Equal(range.Buckets[0].Start, range.FilterStart);
        Assert.False(range.FirstBucketCollectsOlder);
        Assert.DoesNotContain("≤", range.Buckets[0].Label);
    }

    [Fact]
    public void Resolve_EverythingReachesToTheOldestMessageAndHasNoLowerBound()
    {
        var earliest = new DateTime(2022, 4, 7, 13, 12, 11);
        var range = DashboardPeriods.Resolve(PeriodGranularity.Year, Window("all"), _now, earliest);

        Assert.Equal(5, range.Buckets.Count);
        Assert.Equal(new DateTime(2022, 1, 1), range.Buckets[0].Start);
        Assert.Equal(new DateTime(2027, 1, 1), range.EndExclusive);

        // Nothing is excluded at the bottom, so the first bar says that it holds the older
        // mail as well and the bars still add up to the archive.
        Assert.Null(range.FilterStart);
        Assert.True(range.FirstBucketCollectsOlder);
        Assert.StartsWith("≤", range.Buckets[0].Label);
    }

    [Fact]
    public void Resolve_EverythingOnAnEmptyArchiveIsTheCurrentBucketAlone()
    {
        var range = DashboardPeriods.Resolve(PeriodGranularity.Year, Window("all"), _now, null);

        Assert.Single(range.Buckets);
        Assert.Equal(new DateTime(2026, 1, 1), range.Buckets[0].Start);
    }

    [Fact]
    public void Resolve_ABrokenDateCannotStretchTheAxis()
    {
        // A message whose Date header parsed to the year one is not a reason to draw two
        // hundred all but empty bars. The axis stops at twenty years and the first bucket takes
        // everything before it, so a shorter axis hides nothing.
        var range = DashboardPeriods.Resolve(
            PeriodGranularity.Year, Window("all"), _now, new DateTime(1, 1, 1));

        Assert.Equal(DashboardPeriods.MaxYearBucketsForEverything, range.Buckets.Count);
        Assert.Equal(new DateTime(2007, 1, 1), range.Buckets[0].Start);
        Assert.Equal(new DateTime(2026, 1, 1), range.Buckets[^1].Start);
        Assert.StartsWith("\u2264", range.Buckets[0].Label);
        Assert.True(range.FirstBucketCollectsOlder);
        Assert.Null(range.FilterStart);
    }

    [Fact]
    public void Resolve_AnArchiveShorterThanTheCapKeepsItsOwnLength()
    {
        // The cap is an upper bound, not a length: an archive of five years draws five bars.
        var range = DashboardPeriods.Resolve(
            PeriodGranularity.Year, Window("all"), _now, new DateTime(2022, 4, 7));

        Assert.Equal(5, range.Buckets.Count);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(1827, 6, 30)]
    [InlineData(1950, 1, 1)]
    [InlineData(1971, 1, 1)]
    [InlineData(1995, 8, 14)]
    [InlineData(2012, 5, 26)]
    [InlineData(2025, 12, 31)]
    public void Resolve_NoAxisStartsBeforeNetworkMailExisted(int year, int month, int day)
    {
        // An axis reaching behind 1971 is showing a broken Date header and not an archive. The
        // bar cap is what enforces it today; this holds the rule itself, so raising that cap
        // past the distance to 1971 cannot go unnoticed.
        var range = DashboardPeriods.Resolve(
            PeriodGranularity.Year, Window("all"), _now, new DateTime(year, month, day));

        Assert.True(range.Buckets[0].Start.Year >= DashboardPeriods.EarliestEmailYear,
            $"an oldest message of {year:0000}-{month:00}-{day:00} put the axis at {range.Buckets[0].Start.Year}");
        Assert.InRange(range.Buckets.Count, 1, DashboardPeriods.MaxYearBucketsForEverything);
        Assert.True(range.FirstBucketCollectsOlder);
    }

    [Fact]
    public void Resolve_AMessageDatedInTheFutureDoesNotStretchTheAxisEither()
    {
        var range = DashboardPeriods.Resolve(
            PeriodGranularity.Year, Window("all"), _now, new DateTime(2099, 5, 5));

        Assert.Single(range.Buckets);
        Assert.Equal(new DateTime(2027, 1, 1), range.EndExclusive);
    }

    [Fact]
    public void Resolve_NoBoundCarriesAKindThatWouldBeRefusedAsAQueryParameter()
    {
        // The bounds are written against a timestamp column without a time zone, which Npgsql
        // refuses for a DateTime of kind Utc. Wall-clock values of the display timezone arrive
        // here as either kind depending on the caller, and leave as one.
        var range = DashboardPeriods.Resolve(
            PeriodGranularity.Day, Window("7d"), DateTime.SpecifyKind(_now, DateTimeKind.Utc));

        Assert.Equal(DateTimeKind.Unspecified, range.EndExclusive.Kind);
        Assert.Equal(DateTimeKind.Unspecified, range.FilterStart!.Value.Kind);
        Assert.All(range.Buckets, b => Assert.Equal(DateTimeKind.Unspecified, b.Start.Kind));
    }

    [Fact]
    public void Resolve_NeverProducesMoreBarsThanTheCapAllows()
    {
        foreach (var granularity in DashboardPeriods.Granularities)
            foreach (var window in DashboardPeriods.OfferedWindows(granularity))
            {
                var range = DashboardPeriods.Resolve(granularity, window, _now, new DateTime(1990, 1, 1));

                var cap = window.Unit == PeriodWindowUnit.Everything
                    ? DashboardPeriods.MaxYearBucketsForEverything
                    : DashboardPeriods.MaxBuckets;
                Assert.InRange(range.Buckets.Count, 1, cap);
                Assert.Equal(range.Buckets.Count, range.Buckets.Select(b => b.Start).Distinct().Count());
            }
    }

    // ============================================================
    // Paging the window
    // ============================================================

    [Fact]
    public void Offset_OneWindowBackSitsExactlyBelowTheCurrentOne()
    {
        var present = DashboardPeriods.Resolve(
            PeriodGranularity.Day, Window("2d"), _now, new DateTime(2020, 1, 1));
        var earlier = DashboardPeriods.Resolve(
            PeriodGranularity.Day, Window("2d"), _now, new DateTime(2020, 1, 1), offset: -1);

        Assert.Equal(present.Buckets.Count, earlier.Buckets.Count);

        // The windows tile: the earlier one ends where the present one starts.
        Assert.Equal(present.Buckets[0].Start, earlier.EndExclusive);
        Assert.Equal(new DateTime(2026, 9, 15), earlier.Buckets[0].Start);
        Assert.Equal(new DateTime(2026, 9, 16), earlier.Buckets[^1].Start);
    }

    [Fact]
    public void Offset_AMonthWindowStepsByACalendarMonth()
    {
        // Stepping by thirty days would walk the window off the month boundaries; a month
        // window has to step by a month whatever that month is worth in days.
        var earlier = DashboardPeriods.Resolve(
            PeriodGranularity.Day, Window("1m"), _now, new DateTime(2020, 1, 1), offset: -1);

        Assert.Equal(new DateTime(2026, 8, 19), earlier.EndExclusive);
        Assert.Equal(new DateTime(2026, 7, 19), earlier.Buckets[0].Start);
    }

    [Fact]
    public void Offset_AYearWindowStepsByWholeYears()
    {
        var earlier = DashboardPeriods.Resolve(
            PeriodGranularity.Month, Window("1y"), _now, new DateTime(2000, 1, 1), offset: -2);

        Assert.Equal(12, earlier.Buckets.Count);
        Assert.Equal(new DateTime(2023, 10, 1), earlier.Buckets[0].Start);
        Assert.Equal(new DateTime(2024, 9, 1), earlier.Buckets[^1].Start);
    }

    [Fact]
    public void Offset_NothingIsDatedAfterNow()
    {
        var ahead = DashboardPeriods.Resolve(
            PeriodGranularity.Day, Window("7d"), _now, new DateTime(2020, 1, 1), offset: 5);
        var present = DashboardPeriods.Resolve(
            PeriodGranularity.Day, Window("7d"), _now, new DateTime(2020, 1, 1));

        Assert.Equal(0, ahead.Offset);
        Assert.Equal(present.EndExclusive, ahead.EndExclusive);
        Assert.False(ahead.CanGoForward);
    }

    [Fact]
    public void Offset_ReachingPastTheOldestMailIsPulledBackToTheFurthestWindow()
    {
        // Same treatment an unknown resolution gets: the answer is the nearest thing that holds
        // mail, and it says which position that was.
        var earliest = new DateTime(2026, 9, 1);
        var far = DashboardPeriods.Resolve(
            PeriodGranularity.Day, Window("7d"), _now, earliest, offset: -500);

        Assert.True(far.Offset > -500);
        Assert.False(far.CanGoBack);
        Assert.True(far.CanGoForward);

        // Still a whole window of whole buckets, not a stump.
        Assert.Equal(7, far.Buckets.Count);
        Assert.True(far.EndExclusive > earliest,
            "the furthest window has to still meet the oldest mail");
    }

    [Fact]
    public void Offset_AnArchiveShorterThanOneWindowCannotBePaged()
    {
        var earliest = _now.Date.AddDays(-3);
        var range = DashboardPeriods.Resolve(
            PeriodGranularity.Month, Window("1y"), _now, earliest);

        Assert.Equal(0, range.Offset);
        Assert.False(range.CanGoBack);
        Assert.False(range.CanGoForward);
    }

    [Fact]
    public void Offset_ForwardOpensUpAsSoonAsThePresentIsLeft()
    {
        var present = DashboardPeriods.Resolve(
            PeriodGranularity.Day, Window("7d"), _now, new DateTime(2020, 1, 1));
        var earlier = DashboardPeriods.Resolve(
            PeriodGranularity.Day, Window("7d"), _now, new DateTime(2020, 1, 1), offset: -1);

        Assert.False(present.CanGoForward);
        Assert.True(present.CanGoBack);
        Assert.True(earlier.CanGoForward);
    }

    [Fact]
    public void Offset_TheWholeArchiveWindowIsNotPaged()
    {
        var range = DashboardPeriods.Resolve(
            PeriodGranularity.Year, Window("all"), _now, new DateTime(2012, 5, 26), offset: -3);

        Assert.Equal(0, range.Offset);
        Assert.False(range.CanGoBack);
        Assert.False(range.CanGoForward);
    }

    [Fact]
    public void Offset_HowManyPagesThereAreDoesNotDependOnTheBarWidth()
    {
        // Switching the resolution changes how wide the bars are, not how far the window can be
        // moved. Anything else would make the paging limit jump around under the reader.
        var earliest = new DateTime(2024, 3, 3);
        var byWindow = DashboardPeriods.WindowsSinceFloor(Window("7d"), _now, earliest);

        foreach (var granularity in DashboardPeriods.Granularities)
        {
            if (!DashboardPeriods.IsOffered(granularity, Window("7d")))
                continue;

            var range = DashboardPeriods.Resolve(granularity, Window("7d"), _now, earliest, offset: -(byWindow - 1));
            Assert.Equal(-(byWindow - 1), range.Offset);
            Assert.False(range.CanGoBack);
        }
    }

    [Fact]
    public void ArchiveFloor_IsTheOldestMailWhenThatIsInsideTheAxis()
    {
        // Day precise on purpose: the year the axis starts in would let a fresh installation
        // page back through months that never held mail.
        var earliest = new DateTime(2026, 5, 3, 11, 30, 0);
        Assert.Equal(earliest, DashboardPeriods.ArchiveFloor(_now, earliest));
    }

    [Fact]
    public void ArchiveFloor_IsTheAxisStartWhenTheOldestMailIsOlderThanIt()
    {
        var floor = DashboardPeriods.ArchiveFloor(_now, new DateTime(1, 1, 1));
        var axisStart = DashboardPeriods.EverythingAxisStart(_now, new DateTime(1, 1, 1));

        Assert.Equal(axisStart, floor);
        Assert.Equal(new DateTime(2007, 1, 1), floor);
    }

    [Fact]
    public void ArchiveFloor_AnEmptyArchiveHasNothingToPageThrough()
    {
        var floor = DashboardPeriods.ArchiveFloor(_now, null);
        Assert.True(floor > _now);

        var range = DashboardPeriods.Resolve(PeriodGranularity.Day, Window("7d"), _now, null);
        Assert.False(range.CanGoBack);
        Assert.False(range.CanGoForward);
    }

    [Fact]
    public void Label_NamesTheRangeFromItsOwnFirstAndLastBucket()
    {
        var range = DashboardPeriods.Resolve(
            PeriodGranularity.Month, DashboardPeriods.DefaultWindow, _now);

        Assert.Equal($"{range.Buckets[0].Label} – {range.Buckets[^1].Label}", range.Label);
    }

    [Fact]
    public void Resolve_BucketsAreContiguousAndEndWhereTheRangeEnds()
    {
        foreach (var granularity in DashboardPeriods.Granularities)
            foreach (var window in DashboardPeriods.OfferedWindows(granularity))
            {
                var range = DashboardPeriods.Resolve(granularity, window, _now, new DateTime(2019, 6, 1));

                for (var i = 1; i < range.Buckets.Count; i++)
                    Assert.Equal(
                        DashboardPeriods.Step(range.Buckets[i - 1].Start, granularity, 1),
                        range.Buckets[i].Start);

                Assert.Equal(
                    range.EndExclusive,
                    DashboardPeriods.Step(range.Buckets[^1].Start, granularity, 1));
            }
    }
}

using System.Globalization;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// The width of one bar in a dashboard chart.
    /// </summary>
    public enum PeriodGranularity
    {
        Hour,
        Day,
        Month,
        Year
    }

    /// <summary>
    /// How far back a dashboard chart reaches. <see cref="Everything"/> reaches to the oldest
    /// message in the archive and carries no amount.
    /// </summary>
    public enum PeriodWindowUnit
    {
        Day,
        Month,
        Year,
        Everything
    }

    /// <summary>
    /// One entry of the window menu, addressed by its <see cref="Key"/> because that is what
    /// travels in a query string.
    /// </summary>
    public sealed class PeriodWindow
    {
        public PeriodWindow(string key, PeriodWindowUnit unit, int amount)
        {
            Key = key;
            Unit = unit;
            Amount = amount;
        }

        public string Key { get; }
        public PeriodWindowUnit Unit { get; }

        /// <summary>Number of <see cref="Unit"/>s, 0 for <see cref="PeriodWindowUnit.Everything"/>.</summary>
        public int Amount { get; }
    }

    /// <summary>One bar: where it starts and what it is called.</summary>
    public sealed class PeriodBucket
    {
        public PeriodBucket(DateTime start, string label)
        {
            Start = start;
            Label = label;
        }

        public DateTime Start { get; }
        public string Label { get; }
    }

    /// <summary>
    /// A resolved selection: the range to query and the buckets to fold the result into.
    /// </summary>
    public sealed class PeriodRange
    {
        public PeriodRange(
            PeriodGranularity granularity,
            PeriodWindow window,
            DateTime? filterStart,
            DateTime endExclusive,
            IReadOnlyList<PeriodBucket> buckets,
            bool firstBucketCollectsOlder,
            int offset = 0,
            bool canGoBack = false,
            bool canGoForward = false)
        {
            Granularity = granularity;
            Window = window;
            FilterStart = filterStart;
            EndExclusive = endExclusive;
            Buckets = buckets;
            FirstBucketCollectsOlder = firstBucketCollectsOlder;
            Offset = offset;
            CanGoBack = canGoBack;
            CanGoForward = canGoForward;
        }

        public PeriodGranularity Granularity { get; }
        public PeriodWindow Window { get; }

        /// <summary>
        /// Lower bound for the query, or null when the range reaches back as far as the archive
        /// goes. A null bound is why <see cref="FirstBucketCollectsOlder"/> exists: there is no
        /// point below which a message can be left out of the chart silently.
        /// </summary>
        public DateTime? FilterStart { get; }

        /// <summary>
        /// Upper bound, exclusive: the start of the bucket after the one holding the current
        /// instant. Messages dated in the future, which a broken Date header produces, fall
        /// outside the chart rather than stretching its axis.
        /// </summary>
        public DateTime EndExclusive { get; }

        public IReadOnlyList<PeriodBucket> Buckets { get; }

        /// <summary>
        /// Whether the first bucket also holds everything older than it. True whenever the range
        /// has no lower bound, so that the bars add up to the archive no matter how far back it
        /// reaches or what a broken date claims.
        /// </summary>
        public bool FirstBucketCollectsOlder { get; }

        /// <summary>
        /// How many whole windows this range sits before the one holding the current instant.
        /// Zero is that window, negative reaches into the past. Never positive: nothing can be
        /// dated after now except a broken header.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// Whether a window further back would still meet mail. False once the window before
        /// this one lies entirely at or before <see cref="ArchiveFloor"/>, which is also true at
        /// offset zero when the archive is shorter than one window.
        /// </summary>
        public bool CanGoBack { get; }

        /// <summary>Whether this range is not the one holding the current instant.</summary>
        public bool CanGoForward { get; }

        /// <summary>
        /// The range as one line, built from the labels of its own first and last bucket so it
        /// cannot say something the axis does not.
        /// </summary>
        public string Label =>
            Buckets.Count == 0 ? string.Empty
            : Buckets.Count == 1 ? Buckets[0].Label
            : $"{Buckets[0].Label} \u2013 {Buckets[^1].Label}";
    }

    /// <summary>
    /// The granularity and window menus of the dashboard charts, and the resolution of a chosen
    /// pair into a range of buckets.
    /// <para>
    /// Which pairs are offered is derived from two numbers rather than listed by hand: a bar
    /// chart of one bar is a number and not a distribution, and past a couple of hundred bars it
    /// is a smear. The bucket count used for that decision is approximate, because it only has
    /// to decide what the menu offers; the series itself is built with calendar arithmetic.
    /// </para>
    /// </summary>
    public static class DashboardPeriods
    {
        public const int MinBuckets = 2;
        public const int MaxBuckets = 200;

        /// <summary>
        /// How many year bars the window reaching back as far as the archive goes may draw.
        /// <para>
        /// The span of that window is whatever the oldest send date says, and a send date comes
        /// from a Date header: a single broken one dated to the year one would otherwise put two
        /// hundred all but empty bars on the axis. Everything older than the first bar is counted
        /// in it, so a shorter axis hides nothing, and twenty years is a length that can still be
        /// read.
        /// </para>
        /// <para>
        /// The absolute floor behind this number is the year network mail was first sent, 1971: an
        /// axis that starts before it is showing a broken header and not an archive. At twenty
        /// years the cap is what binds, and raising it past the distance to 1971 would bring the
        /// wallpaper back.
        /// </para>
        /// </summary>
        public const int MaxYearBucketsForEverything = 20;

        /// <summary>
        /// Hard stop when counting how many windows fit above the floor. The shortest window
        /// against the furthest floor is one day against twenty years, so this is well above
        /// anything reachable and only there to keep the loop finite.
        /// </summary>
        public const int MaxOffsetWindows = 10000;

        /// <summary>
        /// Year network mail was first sent. No axis may start before it.
        /// </summary>
        public const int EarliestEmailYear = 1971;

        public const PeriodGranularity DefaultGranularity = PeriodGranularity.Month;
        public const string DefaultWindowKey = "1y";

        /// <summary>
        /// Prefix for the label of a first bucket that also holds everything older than itself.
        /// A comparison sign rather than a word, so it needs no translation.
        /// </summary>
        private const string CollectsOlderPrefix = "≤ ";

        private static readonly PeriodWindow[] _windows =
        {
            new("1d",  PeriodWindowUnit.Day,   1),
            new("2d",  PeriodWindowUnit.Day,   2),
            new("5d",  PeriodWindowUnit.Day,   5),
            new("7d",  PeriodWindowUnit.Day,   7),
            new("14d", PeriodWindowUnit.Day,   14),
            new("28d", PeriodWindowUnit.Day,   28),
            new("1m",  PeriodWindowUnit.Month, 1),
            new("2m",  PeriodWindowUnit.Month, 2),
            new("3m",  PeriodWindowUnit.Month, 3),
            new("6m",  PeriodWindowUnit.Month, 6),
            new("1y",  PeriodWindowUnit.Year,  1),
            new("2y",  PeriodWindowUnit.Year,  2),
            new("5y",  PeriodWindowUnit.Year,  5),
            new("10y", PeriodWindowUnit.Year,  10),
            new("all", PeriodWindowUnit.Everything, 0)
        };

        public static IReadOnlyList<PeriodWindow> Windows => _windows;

        public static PeriodWindow DefaultWindow => _windows.First(w => w.Key == DefaultWindowKey);

        public static IReadOnlyList<PeriodGranularity> Granularities { get; } = new[]
        {
            PeriodGranularity.Hour,
            PeriodGranularity.Day,
            PeriodGranularity.Month,
            PeriodGranularity.Year
        };

        /// <summary>
        /// Whether a granularity and a window make a chart worth drawing. Reaching to the oldest
        /// message is offered for whole years only: the span is whatever the archive happens to
        /// hold, so it is the one granularity that cannot produce an unreadable number of bars
        /// for a long-running installation.
        /// </summary>
        public static bool IsOffered(PeriodGranularity granularity, PeriodWindow window)
        {
            if (window.Unit == PeriodWindowUnit.Everything)
                return granularity == PeriodGranularity.Year;

            var buckets = ApproximateBucketCount(granularity, window);
            return buckets >= MinBuckets && buckets <= MaxBuckets;
        }

        public static IReadOnlyList<PeriodWindow> OfferedWindows(PeriodGranularity granularity) =>
            _windows.Where(w => IsOffered(granularity, w)).ToList();

        /// <summary>
        /// Turns whatever arrived in the query string into a pair that is actually offered. Never
        /// throws and never reports back what it was given: an unknown granularity, an unknown
        /// window or a combination that is not offered all fall back to the default, and the
        /// caller tells the browser which pair it ended up with.
        /// </summary>
        public static (PeriodGranularity Granularity, PeriodWindow Window) Canonicalize(
            string? granularity, string? windowKey)
        {
            var match = Granularities
                .Select(g => (PeriodGranularity?)g)
                .FirstOrDefault(g => string.Equals(g!.Value.ToString(), granularity, StringComparison.OrdinalIgnoreCase));
            var resolvedGranularity = match ?? DefaultGranularity;

            var resolvedWindow = _windows
                .FirstOrDefault(w => string.Equals(w.Key, windowKey, StringComparison.OrdinalIgnoreCase));

            if (resolvedWindow == null || !IsOffered(resolvedGranularity, resolvedWindow))
            {
                var defaultWindow = _windows.First(w => w.Key == DefaultWindowKey);

                // Keep the granularity the caller asked for where possible, because that is the
                // coarser of the two choices, and move the window to the offered one closest to
                // what was asked for.
                resolvedWindow = resolvedWindow == null
                    ? (IsOffered(resolvedGranularity, defaultWindow)
                        ? defaultWindow
                        : OfferedWindows(resolvedGranularity).First())
                    : NearestOffered(resolvedGranularity, resolvedWindow);
            }

            return (resolvedGranularity, resolvedWindow);
        }

        /// <summary>
        /// The offered window closest to the given one, measured in menu positions. Used when a
        /// granularity changes underneath a window that it does not offer, so that switching
        /// from months to hours lands on the longest hour window rather than on a default.
        /// </summary>
        public static PeriodWindow NearestOffered(PeriodGranularity granularity, PeriodWindow window)
        {
            var offered = OfferedWindows(granularity);
            if (offered.Count == 0)
                return _windows.First(w => w.Key == DefaultWindowKey);

            var position = Array.FindIndex(_windows, w => w.Key == window.Key);
            return offered
                .OrderBy(w => Math.Abs(Array.FindIndex(_windows, x => x.Key == w.Key) - position))
                .First();
        }

        /// <summary>
        /// Start of the earliest year bar the window over the whole archive draws. Capped at
        /// <see cref="MaxYearBucketsForEverything"/>, which is what keeps a broken Date header
        /// from setting the length of that axis.
        /// </summary>
        public static DateTime EverythingAxisStart(DateTime nowInDisplayTimeZone, DateTime? earliestSentDate)
        {
            nowInDisplayTimeZone = DateTime.SpecifyKind(nowInDisplayTimeZone, DateTimeKind.Unspecified);

            var yearEnd = Step(Truncate(nowInDisplayTimeZone, PeriodGranularity.Year), PeriodGranularity.Year, 1);
            var earliestYear = earliestSentDate.HasValue
                ? Truncate(DateTime.SpecifyKind(earliestSentDate.Value, DateTimeKind.Unspecified), PeriodGranularity.Year)
                : Truncate(nowInDisplayTimeZone, PeriodGranularity.Year);

            var years = CountSteps(earliestYear, yearEnd, PeriodGranularity.Year, MaxYearBucketsForEverything);
            return years > MaxYearBucketsForEverything
                ? Step(yearEnd, PeriodGranularity.Year, -MaxYearBucketsForEverything)
                : Step(yearEnd, PeriodGranularity.Year, -Math.Max(years, 1));
        }

        /// <summary>
        /// How far back paging may reach: the oldest send date, but never before the window over
        /// the whole archive starts.
        /// <para>
        /// Derived from <see cref="EverythingAxisStart"/> rather than decided again, so paging
        /// and that axis cannot say different things about how far the archive goes. The oldest
        /// send date alone would let a single broken Date header open two thousand empty
        /// windows; the axis start alone would be a whole year wide and let a fresh installation
        /// page back through months that never held mail.
        /// </para>
        /// <para>
        /// An archive with no mail at all has no floor to stand on, and paging is then off: the
        /// floor is put after the current instant so that not even one window fits below it.
        /// </para>
        /// </summary>
        public static DateTime ArchiveFloor(DateTime nowInDisplayTimeZone, DateTime? earliestSentDate)
        {
            nowInDisplayTimeZone = DateTime.SpecifyKind(nowInDisplayTimeZone, DateTimeKind.Unspecified);

            if (!earliestSentDate.HasValue)
                return Step(Truncate(nowInDisplayTimeZone, PeriodGranularity.Day), PeriodGranularity.Day, 1);

            var earliest = DateTime.SpecifyKind(earliestSentDate.Value, DateTimeKind.Unspecified);
            var axisStart = EverythingAxisStart(nowInDisplayTimeZone, earliestSentDate);
            return earliest > axisStart ? earliest : axisStart;
        }

        /// <summary>
        /// How many whole windows of the given kind fit between the floor and the end of the
        /// window holding the current instant, itself included. One means only the current
        /// window has anywhere to stand, and paging back is then already at its limit.
        /// </summary>
        /// <remarks>
        /// Counted by stepping because a window of months or years has no fixed length in days.
        /// The step count is bounded: the shortest window over the longest floor distance is a
        /// day against twenty years, so a few thousand additions is the worst case, once per
        /// chart request.
        /// <para>
        /// Deliberately independent of the bar width, so switching the resolution does not
        /// change how many pages there are. Only the width of the bars inside a page changes.
        /// </para>
        /// </remarks>
        public static int WindowsSinceFloor(
            PeriodWindow window, DateTime nowInDisplayTimeZone, DateTime? earliestSentDate)
        {
            if (window.Unit == PeriodWindowUnit.Everything)
                return 1;

            var floor = ArchiveFloor(nowInDisplayTimeZone, earliestSentDate);
            var cursor = Step(
                Truncate(DateTime.SpecifyKind(nowInDisplayTimeZone, DateTimeKind.Unspecified), PeriodGranularity.Day),
                PeriodGranularity.Day, 1);

            var windows = 0;
            while (cursor > floor && windows < MaxOffsetWindows)
            {
                cursor = Subtract(cursor, window, 1);
                windows++;
            }
            return Math.Max(windows, 1);
        }

        /// <summary>
        /// Resolves a selection against the current instant. <paramref name="nowInDisplayTimeZone"/>
        /// has to be the wall-clock time of the configured display timezone, because that is the
        /// timezone archived send dates are stored in: a day bucket cut at UTC midnight would
        /// put the late evening of one day into the next.
        /// </summary>
        /// <param name="earliestSentDate">
        /// The oldest send date in the archive. Sets how far the window over the whole archive
        /// reaches, and how far back paging may go. Null means the archive is empty or the
        /// caller did not look, and the range then holds the current window alone.
        /// </param>
        /// <param name="offset">
        /// Whole windows to move into the past, zero being the window holding the current
        /// instant. A positive value is taken as zero, and a value reaching past the floor is
        /// pulled back to the furthest window that still meets mail: the same treatment an
        /// unknown granularity gets, for the same reason.
        /// </param>
        public static PeriodRange Resolve(
            PeriodGranularity granularity,
            PeriodWindow window,
            DateTime nowInDisplayTimeZone,
            DateTime? earliestSentDate = null,
            int offset = 0)
        {
            // Both bounds end up as query parameters against a "timestamp without time zone"
            // column, which Npgsql refuses to write a DateTime of kind Utc to. The values are
            // wall-clock times of the display timezone either way, so the kind is dropped here
            // rather than left to every caller to get right.
            nowInDisplayTimeZone = DateTime.SpecifyKind(nowInDisplayTimeZone, DateTimeKind.Unspecified);
            if (earliestSentDate.HasValue)
                earliestSentDate = DateTime.SpecifyKind(earliestSentDate.Value, DateTimeKind.Unspecified);

            // The window over the whole archive already holds everything, so there is nothing to
            // page through, and anything after now is a broken header rather than data.
            if (window.Unit == PeriodWindowUnit.Everything || offset > 0)
                offset = 0;

            var windows = WindowsSinceFloor(window, nowInDisplayTimeZone, earliestSentDate);
            if (offset < -(windows - 1))
                offset = -(windows - 1);

            var currentEnd = Step(Truncate(nowInDisplayTimeZone, granularity), granularity, 1);
            var endExclusive = offset == 0
                ? currentEnd
                : Truncate(Subtract(currentEnd, window, -offset), granularity);

            DateTime firstBucketStart;
            DateTime? filterStart;
            bool collectsOlder;

            if (window.Unit == PeriodWindowUnit.Everything)
            {
                // Years is the only granularity this window is offered with, so the year axis is
                // the one that applies and it comes from the shared helper.
                firstBucketStart = granularity == PeriodGranularity.Year
                    ? EverythingAxisStart(nowInDisplayTimeZone, earliestSentDate)
                    : Step(endExclusive, granularity, -Math.Max(
                        CountSteps(
                            earliestSentDate.HasValue
                                ? Truncate(earliestSentDate.Value, granularity)
                                : Truncate(nowInDisplayTimeZone, granularity),
                            endExclusive, granularity),
                        1));

                // No lower bound, so nothing can fall out of the chart at the bottom, and the
                // first bar says that it holds the older mail as well.
                filterStart = null;
                collectsOlder = true;
            }
            else
            {
                firstBucketStart = Truncate(Subtract(endExclusive, window, 1), granularity);
                filterStart = firstBucketStart;
                collectsOlder = false;
            }

            var buckets = new List<PeriodBucket>();
            var culture = CultureInfo.CurrentCulture;
            var cursor = firstBucketStart;
            while (cursor < endExclusive && buckets.Count < MaxBuckets)
            {
                var label = Label(cursor, granularity, culture);
                if (buckets.Count == 0 && collectsOlder)
                    label = CollectsOlderPrefix + label;

                buckets.Add(new PeriodBucket(cursor, label));
                cursor = Step(cursor, granularity, 1);
            }

            return new PeriodRange(
                granularity, window, filterStart, endExclusive, buckets, collectsOlder,
                offset,
                canGoBack: -offset < windows - 1,
                canGoForward: offset < 0);
        }

        /// <summary>
        /// The label of one bucket. Months keep the name-plus-year form the dashboard has always
        /// shown, so the default chart reads exactly as before.
        /// </summary>
        public static string Label(DateTime start, PeriodGranularity granularity, CultureInfo culture) =>
            granularity switch
            {
                PeriodGranularity.Year => start.Year.ToString(culture),
                PeriodGranularity.Month => $"{culture.DateTimeFormat.GetMonthName(start.Month)} {start.Year}",
                PeriodGranularity.Day => start.ToString("d", culture),
                _ => $"{start.ToString("d", culture)} {start.Hour:00}:00"
            };

        /// <summary>
        /// Start of the bucket a moment falls into. The kind is carried over unchanged: these are
        /// wall-clock values of the display timezone, and re-labelling them as UTC here would
        /// shift every boundary by the offset.
        /// </summary>
        public static DateTime Truncate(DateTime value, PeriodGranularity granularity) =>
            granularity switch
            {
                PeriodGranularity.Year => new DateTime(value.Year, 1, 1, 0, 0, 0, value.Kind),
                PeriodGranularity.Month => new DateTime(value.Year, value.Month, 1, 0, 0, 0, value.Kind),
                PeriodGranularity.Day => new DateTime(value.Year, value.Month, value.Day, 0, 0, 0, value.Kind),
                _ => new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, value.Kind)
            };

        public static DateTime Step(DateTime value, PeriodGranularity granularity, int count) =>
            granularity switch
            {
                PeriodGranularity.Year => value.AddYears(count),
                PeriodGranularity.Month => value.AddMonths(count),
                PeriodGranularity.Day => value.AddDays(count),
                _ => value.AddHours(count)
            };

        /// <summary>
        /// Moves back by whole windows. Calendar arithmetic, so a step of one month window is one
        /// calendar month and the windows keep sitting on month boundaries instead of drifting
        /// off them by the length of February.
        /// </summary>
        private static DateTime Subtract(DateTime value, PeriodWindow window, int times) =>
            window.Unit switch
            {
                PeriodWindowUnit.Year => value.AddYears(-window.Amount * times),
                PeriodWindowUnit.Month => value.AddMonths(-window.Amount * times),
                _ => value.AddDays(-window.Amount * times)
            };

        /// <summary>
        /// How many buckets of the given granularity fit between two moments. Counted by stepping
        /// rather than by dividing, because months and years have no fixed length and because a
        /// day has 23 or 25 hours where the display timezone changes its offset.
        /// </summary>
        private static int CountSteps(
            DateTime from, DateTime toExclusive, PeriodGranularity granularity, int stopAfter = MaxBuckets)
        {
            if (from >= toExclusive)
                return 0;

            var steps = 0;
            var cursor = from;
            while (cursor < toExclusive && steps <= stopAfter)
            {
                cursor = Step(cursor, granularity, 1);
                steps++;
            }
            return steps;
        }

        /// <summary>
        /// Bars a pair would produce, near enough to decide whether to offer it. Uses average
        /// month and year lengths, which is why it is not used for anything that gets displayed.
        /// </summary>
        private static double ApproximateBucketCount(PeriodGranularity granularity, PeriodWindow window)
        {
            var days = window.Unit switch
            {
                PeriodWindowUnit.Day => window.Amount,
                PeriodWindowUnit.Month => window.Amount * 30.44,
                _ => window.Amount * 365.25
            };

            return granularity switch
            {
                PeriodGranularity.Hour => days * 24,
                PeriodGranularity.Day => days,
                PeriodGranularity.Month => days / 30.44,
                _ => days / 365.25
            };
        }
    }
}

// Services/Shared/OffloadCutoff.cs
namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Resolves a migration cutoff into the absolute dates a job stores and reports.
    /// <para>
    /// A relative expression such as "the last six months" is turned into a fixed date when the
    /// job is created, and never re-evaluated while it runs. A job can run for a long time and
    /// may be repeated afterwards, and it has to resolve the same set of mail every time; a
    /// stored relative window would drift between runs and would make the job's own reporting
    /// untrue.
    /// </para>
    /// <para>
    /// Cutoffs are interpreted in the configured display timezone, because
    /// <c>ArchivedEmail.SentDate</c> is stored as a naive timestamp that was already converted
    /// into that timezone. That is also the timezone the search UI shows.
    /// </para>
    /// </summary>
    public static class OffloadCutoff
    {
        /// <summary>
        /// The start of the day <paramref name="months"/> months before the given moment.
        /// Inclusive, so mail sent at 00:00 on that day is inside the window.
        /// </summary>
        public static DateTime FromRelativeMonths(DateTime nowInDisplayTimeZone, int months)
        {
            if (months < 0) throw new ArgumentOutOfRangeException(nameof(months));
            return nowInDisplayTimeZone.Date.AddMonths(-months);
        }

        /// <summary>
        /// The start of the day <paramref name="days"/> days before the given moment.
        /// </summary>
        public static DateTime FromRelativeDays(DateTime nowInDisplayTimeZone, int days)
        {
            if (days < 0) throw new ArgumentOutOfRangeException(nameof(days));
            return nowInDisplayTimeZone.Date.AddDays(-days);
        }

        /// <summary>
        /// Normalises an explicitly given cutoff to the start of its day.
        /// </summary>
        public static DateTime FromAbsolute(DateTime date) => date.Date;

        /// <summary>
        /// The inclusive end of an optional upper bound, reproducing the semantics the search
        /// filter already uses (<c>SentDate &lt;= toDate.AddDays(1).AddSeconds(-1)</c>), so the
        /// whole of the given day is inside the window.
        /// </summary>
        public static DateTime ToInclusiveEnd(DateTime date) => date.Date.AddDays(1).AddSeconds(-1);

        /// <summary>
        /// Whether a stored <c>SentDate</c> falls inside the resolved window.
        /// </summary>
        public static bool IsInWindow(DateTime sentDate, DateTime cutoffFrom, DateTime? cutoffTo)
        {
            if (sentDate < cutoffFrom) return false;
            if (cutoffTo.HasValue && sentDate > ToInclusiveEnd(cutoffTo.Value)) return false;
            return true;
        }
    }
}

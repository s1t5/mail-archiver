using MailArchiver.Models;
using Microsoft.Extensions.Options;

namespace MailArchiver.Utilities
{
    public class DateTimeHelper
    {
        /// <summary>
        /// Lower bound applied to every timestamp that is written to
        /// <c>ArchivedEmail.SentDate</c> or used in a duplicate comparison.
        /// </summary>
        /// <remarks>
        /// A mail server may emit a <c>Date:</c> header that MimeKit cannot represent, in which
        /// case it yields <see cref="DateTimeOffset.MinValue"/> (year 1) instead of failing. Such
        /// a value is not only implausible as a send date, it is also dangerous to compare: the
        /// duplicate query subtracts the stored <c>SentDate</c> from the incoming one, and
        /// PostgreSQL's <c>interval</c> type spans fewer years than <c>timestamp</c> does, so the
        /// difference of two individually valid timestamps can raise
        /// <c>22008: interval out of range</c> and abort the whole import job.
        /// </remarks>
        public static readonly DateTime MinimumPlausibleSentDate =
            new(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        /// <summary>
        /// Upper bound applied to <c>ArchivedEmail.SentDate</c>; see
        /// <see cref="MinimumPlausibleSentDate"/>.
        /// </summary>
        public static readonly DateTime MaximumPlausibleSentDate =
            new(2100, 12, 31, 23, 59, 59, DateTimeKind.Unspecified);

        private readonly TimeZoneInfo _displayTimeZone;

        public DateTimeHelper(IOptions<TimeZoneOptions> timeZoneOptions)
        {
            var timeZoneId = timeZoneOptions.Value.DisplayTimeZoneId;
            _displayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }

        /// <summary>
        /// Converts a DateTimeOffset from any timezone to the configured display timezone
        /// </summary>
        /// <param name="dateTimeOffset">The DateTimeOffset to convert</param>
        /// <returns>DateTime in the configured display timezone</returns>
        public DateTime ConvertToDisplayTimeZone(DateTimeOffset dateTimeOffset)
        {
            return ClampToPlausibleRange(
                TimeZoneInfo.ConvertTime(dateTimeOffset, _displayTimeZone).DateTime);
        }

        /// <summary>
        /// Restricts a stored-format timestamp to <see cref="MinimumPlausibleSentDate"/> through
        /// <see cref="MaximumPlausibleSentDate"/>.
        /// <para>
        /// Applied on both sides of the duplicate comparison - the value about to be written and
        /// the value read from the column - so the two remain comparable, and so that a
        /// timestamp already sitting in the archive outside the range cannot push the
        /// <c>SentDate</c> subtraction past the range of PostgreSQL's <c>interval</c> type.
        /// </para>
        /// </summary>
        /// <param name="dateTime">A timestamp in the display timezone.</param>
        /// <returns>
        /// <paramref name="dateTime"/> when it lies inside the range, otherwise the nearest bound.
        /// </returns>
        public static DateTime ClampToPlausibleRange(DateTime dateTime)
        {
            if (dateTime < MinimumPlausibleSentDate)
                return MinimumPlausibleSentDate;

            if (dateTime > MaximumPlausibleSentDate)
                return MaximumPlausibleSentDate;

            return dateTime;
        }

        /// <summary>
        /// Converts a DateTime to the configured display timezone (assumes it's already in the correct timezone if unspecified)
        /// </summary>
        /// <param name="dateTime">The DateTime to convert</param>
        /// <returns>DateTime in the configured display timezone</returns>
        public DateTime ConvertToDisplayTimeZone(DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
            {
                return TimeZoneInfo.ConvertTimeFromUtc(dateTime, _displayTimeZone);
            }
            else if (dateTime.Kind == DateTimeKind.Local)
            {
                return TimeZoneInfo.ConvertTime(dateTime, _displayTimeZone);
            }
            else
            {
                // Unspecified - assume it's already in the correct timezone
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
            }
        }

        public static DateTime EnsureUtc(DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Unspecified)
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
                
            if (dateTime.Kind == DateTimeKind.Local)
                return dateTime.ToUniversalTime();
                
            return dateTime; // Already UTC
        }

        /// <summary>
        /// Builds a <see cref="DateTimeOffset"/> for a <see cref="DateTime"/> value that is
        /// stored in the configured display timezone (e.g. <c>ArchivedEmail.SentDate</c> after
        /// it round-tripped through PostgreSQL <c>timestamp without time zone</c>, which strips
        /// the <see cref="DateTimeKind"/>). The returned offset is the display timezone's UTC
        /// offset for the given instant, so that downstream consumers (e.g. MimeKit's
        /// <c>MimeMessage.Date</c>) emit a correct <c>Date:</c> header with the proper offset.
        /// </summary>
        /// <param name="dateTime">
        /// A <see cref="DateTime"/> interpreted as local time in the configured display timezone.
        /// </param>
        /// <returns>
        /// A <see cref="DateTimeOffset"/> whose wall-clock time matches <paramref name="dateTime"/>
        /// and whose offset reflects the configured display timezone.
        /// </returns>
        public DateTimeOffset ToDisplayTimeZoneOffset(DateTime dateTime)
        {
            var unspecified = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
            return new DateTimeOffset(unspecified, _displayTimeZone.GetUtcOffset(unspecified));
        }

        /// <summary>
        /// Inverse of <see cref="ConvertToDisplayTimeZone(DateTime)"/>.
        /// Interprets a DateTime stored in the configured display timezone (or with
        /// <see cref="DateTimeKind.Unspecified"/> because it has round-tripped through
        /// PostgreSQL, which strips the kind information for <c>timestamp without time zone</c>
        /// columns) and returns the equivalent UTC DateTime.
        /// Values explicitly marked as <see cref="DateTimeKind.Utc"/> are passed through
        /// unchanged; <see cref="DateTimeKind.Local"/> values are converted via the OS.
        /// </summary>
        /// <param name="dateTime">The DateTime value to convert</param>
        /// <returns>The equivalent UTC DateTime (Kind=Utc)</returns>
        public DateTime ConvertFromDisplayTimeZoneToUtc(DateTime dateTime)
        {
            if (dateTime.Kind == DateTimeKind.Utc)
                return dateTime;

            if (dateTime.Kind == DateTimeKind.Local)
                return dateTime.ToUniversalTime();

            // Unspecified - assume it is in the configured display timezone
            var unspecified = DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, _displayTimeZone);
        }

    }
}

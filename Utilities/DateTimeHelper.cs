using MailArchiver.Models;
using Microsoft.Extensions.Options;

namespace MailArchiver.Utilities
{
    public class DateTimeHelper
    {
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
            return TimeZoneInfo.ConvertTime(dateTimeOffset, _displayTimeZone).DateTime;
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

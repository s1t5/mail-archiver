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
    }
}

// Services/Shared/InternalDateResolver.cs
using MailArchiver.Models;
using MailArchiver.Utilities;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Works out the IMAP INTERNALDATE to append an archived message with, i.e. when it was
    /// delivered.
    /// <para>
    /// This used to be taken from <c>ReceivedDate</c>, which is assigned <c>DateTime.UtcNow</c>
    /// when a message is archived and is therefore the time of the archiving run, not of
    /// delivery. Every restored message consequently carried the date of whichever sync or
    /// import archived it. The defect hides well, because the Date header is correct and clients
    /// sort by it; what was wrong is IMAP SORT ARRIVAL, server side age and retention rules, and
    /// any verification of a restore by date.
    /// </para>
    /// </summary>
    public static class InternalDateResolver
    {
        private const string ReceivedHeaderPrefix = "Received:";

        /// <summary>
        /// The delivery time for a message, preferring its Received chain and falling back to
        /// <c>SentDate</c>.
        /// <para>
        /// The two branches need opposite timezone treatment, which is the easiest thing here to
        /// get backwards, and getting it backwards produces no error at all. A time recovered
        /// from a Received header is an absolute instant and must be used as it is.
        /// <c>SentDate</c> is stored display-local and naive, so it has to be converted out of
        /// the display timezone first.
        /// </para>
        /// </summary>
        public static DateTimeOffset Resolve(ArchivedEmail email, DateTimeHelper dateTimeHelper)
        {
            var delivered = TryExtractDeliveryTime(email.RawHeaders);
            if (delivered.HasValue)
            {
                return delivered.Value;
            }

            var utc = dateTimeHelper.ConvertFromDisplayTimeZoneToUtc(email.SentDate);
            return new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeSpan.Zero);
        }

        /// <summary>
        /// Reads the delivery time out of a stored Received chain, or null if there is none to
        /// read.
        /// <para>
        /// The <b>topmost</b> Received header is the one that matters: the chain is prepended to
        /// as a message travels, so the first one is the final delivery.
        /// </para>
        /// <para>
        /// <see cref="MailContentHelper.ExtractEmailDate"/> cannot be reused here even though it
        /// looks like exactly the right helper. It returns the Date header whenever one is
        /// present, which is the value already stored in <c>SentDate</c>, so the change would
        /// compile, run, and alter nothing. It also deliberately walks the chain from the oldest
        /// hop, because its job is to guess when a message was written, and that is the opposite
        /// end of the chain from the one INTERNALDATE needs.
        /// </para>
        /// <para>
        /// <c>RawHeaders</c> is stored as one "Field: Value" line per header and truncated at
        /// 100,000 characters. Received headers sit at the top of a message, so they normally
        /// survive that truncation.
        /// </para>
        /// </summary>
        public static DateTimeOffset? TryExtractDeliveryTime(string? rawHeaders)
        {
            if (string.IsNullOrEmpty(rawHeaders)) return null;

            foreach (var rawLine in rawHeaders.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (!line.StartsWith(ReceivedHeaderPrefix, StringComparison.OrdinalIgnoreCase)) continue;

                var value = line.Substring(ReceivedHeaderPrefix.Length);
                var parsed = MailContentHelper.ExtractDateFromReceivedHeader(value);
                if (parsed.HasValue) return parsed.Value;
            }

            return null;
        }
    }
}

// Services/Shared/OffloadMatchKey.cs
using MailArchiver.Models;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Builds the two duplicate detection keys an offload uses against a target mailbox.
    /// <para>
    /// Keys are 64 bit hashes rather than strings. Two string sets holding the configured
    /// maximum of 500,000 entries each would cost a few hundred megabytes once .NET string
    /// overhead is counted; the hashes cost single digit megabytes. At that size the birthday
    /// collision probability is negligible, and the consequence of a collision is one message
    /// skipped as already present.
    /// </para>
    /// <para>
    /// The fingerprint deliberately does <b>not</b> mirror the duplicate query the import uses
    /// in <c>MailImporter</c>. That query compares against values it never stored: it joins
    /// addresses with "," while the row was written with ", ", and it compares a raw subject
    /// against a column that went through <see cref="MailContentHelper.CleanText"/>. As a
    /// result its own second criterion cannot match any message with two or more senders or
    /// recipients. Since the database side of the key is fixed by what is already in the
    /// column, the other side has to reproduce the <b>storage</b> format instead.
    /// </para>
    /// </summary>
    public static class OffloadMatchKey
    {
        /// <summary>
        /// The window within which two timestamps count as the same message, matching the
        /// tolerance the import has always used.
        /// </summary>
        public const double TimestampToleranceSeconds = 2;

        // Field size limits, mirroring the ones MailImporter applies when it writes the row.
        private const int FromMaxBytes = 10_000;
        private const int ToMaxBytes = 50_000;
        private const int SubjectMaxBytes = 50_000;

        private const string NoSubject = "(No Subject)";

        // Separates the fields inside a fingerprint so that moving text from one field to the
        // next cannot produce the same key. A unit separator cannot occur in a stored value,
        // because CleanText has already replaced every character below 32 with a space.
        private const string FieldSeparator = "\u001F";

        /// <summary>
        /// Key for the first criterion. Returns null when the stored Message-ID is missing or
        /// carries no "@", because such a value is not emitted on append either: the restore
        /// path drops it and MimeKit generates a fresh random Message-Id instead. A null here
        /// is what pushes the caller on to the fingerprint.
        /// </summary>
        public static long? MessageIdKey(string? messageId)
        {
            var normalized = MailContentHelper.NormalizeMessageId(messageId);
            if (string.IsNullOrEmpty(normalized) || !normalized.Contains('@')) return null;
            return Hash64(normalized.ToLowerInvariant());
        }

        /// <summary>
        /// Fingerprint built from values already in storage format, i.e. straight from the
        /// columns of an archived row.
        /// </summary>
        public static long FingerprintKeyFromStored(string? from, string? to, string? subject)
            => Hash64(string.Concat(
                from ?? string.Empty, FieldSeparator,
                to ?? string.Empty, FieldSeparator,
                subject ?? string.Empty));

        /// <summary>
        /// Convenience overload for an archived row, whose columns are already normalised.
        /// </summary>
        public static long FingerprintKeyFromStored(ArchivedEmail email)
            => FingerprintKeyFromStored(email.From, email.To, email.Subject);

        /// <summary>
        /// Fingerprint built from raw address lists and a raw subject, for example from an IMAP
        /// ENVELOPE. The inputs are normalised into the storage format first, so the result is
        /// comparable with <see cref="FingerprintKeyFromStored(string, string, string)"/> for
        /// the same message.
        /// </summary>
        public static long FingerprintKeyFromAddresses(
            IEnumerable<string>? fromAddresses,
            IEnumerable<string>? toAddresses,
            string? subject)
            => FingerprintKeyFromStored(
                NormalizeAddresses(fromAddresses, FromMaxBytes),
                NormalizeAddresses(toAddresses, ToMaxBytes),
                NormalizeSubject(subject));

        /// <summary>
        /// Joins and cleans addresses exactly as MailImporter does when it writes the column:
        /// ", " between addresses, then <see cref="MailContentHelper.CleanText"/>, then the
        /// tsvector field truncation.
        /// </summary>
        public static string NormalizeAddresses(IEnumerable<string>? addresses, int maxBytes)
        {
            var joined = string.Join(", ", (addresses ?? Enumerable.Empty<string>())
                .Where(a => !string.IsNullOrEmpty(a)));
            return MailContentHelper.TruncateFieldForTsvector(
                MailContentHelper.CleanText(joined), maxBytes);
        }

        /// <summary>
        /// Applies the same subject handling MailImporter applies, including its
        /// "(No Subject)" substitution for a missing subject.
        /// <para>
        /// The substitution is for a <b>null</b> subject only, not for an empty one, because
        /// MailImporter writes <c>message.Subject ?? "(No Subject)"</c>. A message carrying an
        /// empty Subject header is stored with an empty subject, so treating empty as missing
        /// here would make this key disagree with the column for exactly those messages.
        /// </para>
        /// </summary>
        public static string NormalizeSubject(string? subject)
            => MailContentHelper.TruncateFieldForTsvector(
                MailContentHelper.CleanText(subject ?? NoSubject),
                SubjectMaxBytes);

        /// <summary>
        /// Whether two send times are close enough to be the same message. This is a range
        /// comparison and cannot be folded into a hash: quantising into two second buckets is
        /// not equivalent, because two copies one second apart can fall into different buckets,
        /// in which case the match silently never fires.
        /// </summary>
        public static bool WithinTolerance(DateTime a, DateTime b)
            => Math.Abs((a - b).TotalSeconds) < TimestampToleranceSeconds;

        /// <summary>
        /// FNV-1a. Chosen over <see cref="string.GetHashCode()"/> because that is randomised
        /// per process, which would make these keys neither testable nor reproducible.
        /// </summary>
        public static long Hash64(string value)
        {
            const ulong offsetBasis = 14695981039346656037;
            const ulong prime = 1099511628211;

            var hash = offsetBasis;
            foreach (var c in value)
            {
                hash ^= (byte)(c & 0xFF);
                hash *= prime;
                hash ^= (byte)((c >> 8) & 0xFF);
                hash *= prime;
            }
            return unchecked((long)hash);
        }
    }
}

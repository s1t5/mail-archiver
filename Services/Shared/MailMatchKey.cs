// Services/Shared/MailMatchKey.cs
using MailArchiver.Models;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Builds the two duplicate detection keys used to decide whether a message is already
    /// present: an exact Message-ID key, and a fingerprint over sender, recipients, subject and
    /// send time for the messages whose Message-ID cannot carry the decision.
    /// <para>
    /// Keys are 64 bit hashes rather than strings. Two string sets holding the configured
    /// maximum of 500,000 entries each would cost a few hundred megabytes once .NET string
    /// overhead is counted; the hashes cost single digit megabytes. At that size the birthday
    /// collision probability is negligible, and the consequence of a collision is one message
    /// skipped as already present.
    /// </para>
    /// <para>
    /// Every key is built from the <b>storage</b> format, i.e. the form a value takes once it is
    /// in the column, never from the form it happened to arrive in. The database side of a
    /// comparison is fixed by what was already written, so the other side has to reproduce it
    /// exactly. Getting this wrong is not a hypothetical: the archiving pipelines used to join
    /// addresses with "," while the row was written with ", ", and compare a raw subject against
    /// a column that had been through <see cref="MailContentHelper.CleanText"/>, which left
    /// their second criterion unable to match any message with two or more senders or
    /// recipients. Both now normalize through this class.
    /// </para>
    /// </summary>
    public static class MailMatchKey
    {
        /// <summary>
        /// The window within which two timestamps count as the same message, matching the
        /// tolerance the import has always used.
        /// </summary>
        public const double TimestampToleranceSeconds = 2;

        // Field size limits, mirroring the ones the archiving pipelines apply when they write
        // the row. Public because the duplicate queries have to normalize with the same bounds.
        public const int FromMaxBytes = 10_000;
        public const int ToMaxBytes = 50_000;
        public const int SubjectMaxBytes = 50_000;

        private const string NoSubject = "(No Subject)";

        // Separates the fields inside a fingerprint so that moving text from one field to the
        // next cannot produce the same key. A unit separator cannot occur in a stored value,
        // because CleanText has already replaced every character below 32 with a space.
        private const string FieldSeparator = "\u001F";

        /// <summary>
        /// Key for the first criterion, built from the identifier the row is actually appended
        /// with rather than from the raw column.
        /// <para>
        /// Returns null only when there is no stored Message-ID at all, since nothing is emitted
        /// on append in that case and the caller has to fall through to the fingerprint. A
        /// stored value with no "@" no longer yields null: it used to, because the restore path
        /// dropped such values and let a fresh random one be generated, but
        /// <see cref="MailContentHelper.ToRestorableMessageId"/> now derives a stable identifier
        /// from it, so it can carry the match after all.
        /// </para>
        /// </summary>
        public static long? MessageIdKey(string? messageId)
        {
            var restorable = MailContentHelper.ToRestorableMessageId(messageId);
            if (string.IsNullOrEmpty(restorable)) return null;
            return Hash64(restorable.ToLowerInvariant());
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

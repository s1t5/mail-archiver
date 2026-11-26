using System.Linq.Expressions;
using MailArchiver.Models;
using MailArchiver.Utilities;
using Microsoft.EntityFrameworkCore;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Message-ID duplicate check. Some senders reuse a Message-ID for different mails (#622),
    /// so a match also needs the same subject and a send date within one day. If either date
    /// is unknown, the subject is enough. Generated fallback IDs match on the ID alone.
    /// <para>
    /// <see cref="Candidates"/> filters by ID and date in SQL. <see cref="Confirms"/> then
    /// checks the subject in memory, with the same comparison the restore index uses.
    /// </para>
    /// </summary>
    public sealed class MessageIdMatch
    {
        private readonly bool _isHeaderMessageId;
        private readonly string _subject;

        /// <param name="messageIdCandidates">All stored forms of the ID.</param>
        /// <param name="isHeaderMessageId">False for a generated fallback ID.</param>
        /// <param name="subject">Incoming subject, raw or normalized.</param>
        /// <param name="sentDate">Incoming send date in display timezone, or null if unknown.</param>
        public MessageIdMatch(
            IEnumerable<string> messageIdCandidates, bool isHeaderMessageId, string? subject, DateTime? sentDate)
        {
            _isHeaderMessageId = isHeaderMessageId;
            _subject = MailMatchKey.LooseSubject(MailMatchKey.NormalizeSubject(subject));
            Candidates = BuildCandidates(messageIdCandidates.ToList(), isHeaderMessageId, sentDate);
        }

        /// <summary>
        /// Rows with a matching ID and send date. Stored dates outside the plausible range
        /// count as unknown and match.
        /// </summary>
        public Expression<Func<ArchivedEmail, bool>> Candidates { get; }

        /// <summary>Whether the stored subject matches the incoming one.</summary>
        public bool Confirms(string? storedSubject)
            => !_isHeaderMessageId || MailMatchKey.LooseSubject(storedSubject) == _subject;

        /// <summary>
        /// First row whose subject matches. <paramref name="rows"/> must already be filtered by
        /// <see cref="Candidates"/> (optionally OR the fingerprint).
        /// </summary>
        public async Task<T?> FirstConfirmedAsync<T>(
            IQueryable<T> rows, Func<T, string?> subjectOf, CancellationToken cancellationToken = default)
            where T : class
        {
            foreach (var row in await rows.ToListAsync(cancellationToken))
            {
                if (Confirms(subjectOf(row))) return row;
            }
            return null;
        }

        /// <summary>Whether any row's subject matches. Same input as <see cref="FirstConfirmedAsync{T}"/>.</summary>
        public async Task<bool> AnyConfirmedAsync(
            IQueryable<ArchivedEmail> rows, CancellationToken cancellationToken = default)
        {
            var subjects = await rows.Select(e => e.Subject).ToListAsync(cancellationToken);
            return subjects.Any(Confirms);
        }

        private static Expression<Func<ArchivedEmail, bool>> BuildCandidates(
            List<string> candidates, bool isHeaderMessageId, DateTime? sentDate)
        {
            if (!isHeaderMessageId || !sentDate.HasValue || MailMatchKey.IsUnknownSentDate(sentDate.Value))
                return e => candidates.Contains(e.MessageId);

            var minSentDate = DateTimeHelper.MinimumPlausibleSentDate;
            var maxSentDate = DateTimeHelper.MaximumPlausibleSentDate;
            var lower = sentDate.Value.AddSeconds(-MailMatchKey.MessageIdDateToleranceSeconds);
            var upper = sentDate.Value.AddSeconds(MailMatchKey.MessageIdDateToleranceSeconds);

            return e => candidates.Contains(e.MessageId) &&
                        (e.SentDate <= minSentDate || e.SentDate >= maxSentDate ||
                         (e.SentDate > lower && e.SentDate < upper));
        }
    }
}

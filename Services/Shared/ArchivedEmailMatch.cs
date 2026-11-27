using System.Linq.Expressions;
using MailArchiver.Models;
using MailArchiver.Utilities;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Duplicate check predicates for queries on archived emails. Values must be in storage
    /// format. The Message-ID criterion is in <see cref="MessageIdMatch"/>.
    /// <para>
    /// SentDate is compared as a range, not as a difference, so the (MailAccountId, SentDate)
    /// index can be used and old out-of-range dates cannot overflow the interval type (#600).
    /// </para>
    /// </summary>
    public static class ArchivedEmailMatch
    {
        /// <summary>
        /// Same sender, recipients and subject, sent within
        /// <see cref="MailMatchKey.TimestampToleranceSeconds"/>. If the window reaches past the
        /// plausible date range, that side stays open, as if the column were clamped.
        /// </summary>
        public static Expression<Func<ArchivedEmail, bool>> ByFingerprint(
            string normalizedFrom, string normalizedTo, string normalizedSubject, DateTime sentDate)
        {
            var lower = sentDate.AddSeconds(-MailMatchKey.TimestampToleranceSeconds);
            var upper = sentDate.AddSeconds(MailMatchKey.TimestampToleranceSeconds);

            Expression<Func<ArchivedEmail, bool>> match = e =>
                e.From == normalizedFrom && e.To == normalizedTo && e.Subject == normalizedSubject;

            if (lower >= DateTimeHelper.MinimumPlausibleSentDate)
                match = And(match, e => e.SentDate > lower);
            if (upper <= DateTimeHelper.MaximumPlausibleSentDate)
                match = And(match, e => e.SentDate < upper);

            return match;
        }

        /// <summary>Combines two predicates with OR.</summary>
        public static Expression<Func<ArchivedEmail, bool>> Or(
            Expression<Func<ArchivedEmail, bool>> left, Expression<Func<ArchivedEmail, bool>> right)
            => Combine(left, right, Expression.OrElse);

        private static Expression<Func<ArchivedEmail, bool>> And(
            Expression<Func<ArchivedEmail, bool>> left, Expression<Func<ArchivedEmail, bool>> right)
            => Combine(left, right, Expression.AndAlso);

        private static Expression<Func<ArchivedEmail, bool>> Combine(
            Expression<Func<ArchivedEmail, bool>> left, Expression<Func<ArchivedEmail, bool>> right,
            Func<Expression, Expression, BinaryExpression> combine)
        {
            var parameter = left.Parameters[0];
            var rightBody = new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body);
            return Expression.Lambda<Func<ArchivedEmail, bool>>(combine(left.Body, rightBody), parameter);
        }

        private sealed class ParameterReplacer : ExpressionVisitor
        {
            private readonly ParameterExpression _from;
            private readonly ParameterExpression _to;

            public ParameterReplacer(ParameterExpression from, ParameterExpression to)
            {
                _from = from;
                _to = to;
            }

            protected override Expression VisitParameter(ParameterExpression node)
                => node == _from ? _to : base.VisitParameter(node);
        }
    }
}

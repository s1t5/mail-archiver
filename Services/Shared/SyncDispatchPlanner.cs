namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Decides which accounts a scheduler tick may start, and in which order.
    ///
    /// The sync loop used to build this list once per cycle and then wait for the whole batch. One
    /// six-hour mailbox therefore held every other account back, however many slots were configured:
    /// the slots came free, but nothing looked at the list again until the last task returned. The
    /// tick now dispatches into free slots and returns, which puts two rules in front of it that the
    /// batch shape never needed.
    ///
    /// An account already running must not be started a second time. It is skipped here rather than
    /// by clearing its due time, because its due time staying in the past is exactly what keeps it at
    /// the front of the queue once it finishes.
    ///
    /// And with more accounts due than slots free, order decides who waits. Most overdue first is the
    /// whole of the fairness story: without it a slow account near the end of the enumeration can be
    /// passed over tick after tick while accounts that were due later keep taking the slots.
    ///
    /// Pure (no I/O, no static state) so it can be unit-tested in isolation.
    /// </summary>
    public static class SyncDispatchPlanner
    {
        /// <summary>
        /// The accounts that may start now, most overdue first.
        /// </summary>
        /// <param name="candidates">Every enabled account with the time it is next due.</param>
        /// <param name="nowUtc">The tick's timestamp.</param>
        /// <param name="running">Ids of accounts whose sync is already in flight.</param>
        public static IReadOnlyList<int> SelectDueAccounts(
            IEnumerable<(int AccountId, DateTime NextRunUtc)> candidates,
            DateTime nowUtc,
            IReadOnlySet<int> running)
        {
            return candidates
                .Where(c => c.NextRunUtc <= nowUtc)
                .Where(c => !running.Contains(c.AccountId))
                .OrderBy(c => c.NextRunUtc)
                .ThenBy(c => c.AccountId)
                .Select(c => c.AccountId)
                .ToList();
        }
    }
}

namespace MailArchiver.Services.Providers.Imap
{
    /// <summary>
    /// Tracks consecutive reconnect/re-authentication failures during an IMAP folder sync
    /// and decides whether to abort the folder gracefully (analogous to the transient
    /// FETCH failure threshold). Also tracks whether the most recent failure was a parser-
    /// level <c>ImapProtocolException</c> from <c>GetMessageAsync</c>, in which case the
    /// session is likely still usable and the next UID should skip the reconnect gate.
    ///
    /// This class is pure (no I/O, no static state) so it can be unit-tested in isolation.
    /// </summary>
    public sealed class ReconnectCircuitBreaker
    {
        private readonly int _maxConsecutiveFailures;

        /// <summary>
        /// Number of consecutive reconnect/re-auth failures recorded since the last
        /// success. Reset only by a successful reconnect/re-auth (Option A: strict).
        /// Successful FETCHes do NOT reset this counter.
        /// </summary>
        public int ConsecutiveFailures { get; private set; }

        /// <summary>
        /// When true, the next per-message iteration should skip the
        /// <c>IsConnected</c>/<c>IsAuthenticated</c> gate and attempt the FETCH directly,
        /// because the previous failure was a parser error that likely did not break the
        /// session. Consumed (reset to false) after one iteration.
        /// </summary>
        public bool SkipNextReconnectGate { get; private set; }

        public ReconnectCircuitBreaker(int maxConsecutiveFailures)
        {
            _maxConsecutiveFailures = maxConsecutiveFailures;
        }

        /// <summary>
        /// Records a successful reconnect or re-authentication. Resets the failure counter.
        /// </summary>
        public void RecordSuccess()
        {
            ConsecutiveFailures = 0;
        }

        /// <summary>
        /// Records a failed reconnect or re-authentication. Does NOT affect the
        /// <see cref="SkipNextReconnectGate"/> flag (parse errors are handled separately).
        /// </summary>
        /// <returns>True if the threshold has been reached and the folder sync should abort.</returns>
        public bool RecordFailure()
        {
            ConsecutiveFailures++;
            return ConsecutiveFailures >= _maxConsecutiveFailures;
        }

        /// <summary>
        /// Records that the most recent failure was a parser-level
        /// <c>ImapProtocolException</c> (e.g. "Unexpected atom token"). Sets
        /// <see cref="SkipNextReconnectGate"/> so the next UID attempts a direct FETCH
        /// without triggering an unnecessary reconnect. Does NOT increment the reconnect
        /// failure counter — the session is assumed still usable.
        /// </summary>
        public void RecordParseError()
        {
            SkipNextReconnectGate = true;
        }

        /// <summary>
        /// Consumes and resets the <see cref="SkipNextReconnectGate"/> flag. Called by the
        /// sync loop after the gate has been bypassed for one iteration.
        /// </summary>
        public void ConsumeSkipGate()
        {
            SkipNextReconnectGate = false;
        }

        /// <summary>True when the failure threshold has been reached.</summary>
        public bool ShouldAbort => ConsecutiveFailures >= _maxConsecutiveFailures;
    }
}
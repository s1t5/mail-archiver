using MailArchiver.Models;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Why a running sync should stop before it has worked through every folder.
    /// </summary>
    public enum SyncStopReason
    {
        /// <summary>Nothing to do, the sync continues.</summary>
        None,

        /// <summary>Somebody cancelled the job from the UI.</summary>
        Cancelled,

        /// <summary>The account's configured sync timeout elapsed.</summary>
        TimedOut
    }

    /// <summary>
    /// Decides whether a running sync has to stop, and which of the two reasons applies.
    ///
    /// The sync loops already asked <c>SyncJob.Status</c> at three levels — per folder, per batch and
    /// before every single message — but that status is only ever set by an explicit cancel from the
    /// UI. The per-account timeout lives in a separate <see cref="System.Threading.CancellationToken"/>
    /// that no loop consulted, so <c>MailSync:TimeoutMinutes</c> had no effect at all. Both signals now
    /// meet here, so the loops keep one condition instead of two and both providers answer the question
    /// the same way.
    ///
    /// A cancel outranks a timeout when both are true: somebody deliberately stopped this job, and that
    /// is the more specific statement about what happened. The two are not interchangeable afterwards —
    /// a cancel ends the job as failed, a timeout ends it as a pause that keeps its checkpoints.
    ///
    /// Pure (no I/O, no static state) so it can be unit-tested in isolation.
    /// </summary>
    public static class SyncInterruption
    {
        /// <summary>
        /// The reason this sync should stop, or <see cref="SyncStopReason.None"/> to carry on.
        /// </summary>
        /// <param name="jobStatus">
        /// Current status of the sync job, or null when the sync runs without a job (no UI cancel
        /// is possible then, but the timeout still applies).
        /// </param>
        /// <param name="timeoutRequested">Whether the account's sync timeout has elapsed.</param>
        public static SyncStopReason Evaluate(SyncJobStatus? jobStatus, bool timeoutRequested)
        {
            if (jobStatus == SyncJobStatus.Cancelled)
                return SyncStopReason.Cancelled;

            if (timeoutRequested)
                return SyncStopReason.TimedOut;

            return SyncStopReason.None;
        }
    }
}

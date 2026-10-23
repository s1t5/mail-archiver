using MailArchiver.Models;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Whether a job whose sync ended without finishing has to be closed by the caller.
    ///
    /// A sync that throws leaves its <see cref="SyncJob"/> where it was, and where it was is
    /// <see cref="SyncJobStatus.Running"/>. Nothing clears that afterwards: the job dictionary is
    /// in memory and its cleanup only removes jobs that carry a completion timestamp, so the entry
    /// stays for the life of the process. The Jobs page keeps showing the sync as running, and
    /// <c>IsAccountSyncing</c> keeps answering true for that account, which the account list and
    /// the dashboard render as "sync in progress" and the scheduler reads as "do not start this
    /// one".
    ///
    /// The counter-rule matters just as much: a sync that ended itself as TimedOut, Cancelled,
    /// Failed or RateLimited has already made a more precise statement than "something threw".
    /// Overwriting it would replace the one piece of information worth keeping - a timeout that
    /// keeps its checkpoints reads very differently from a failure that does not.
    ///
    /// Pure (no I/O, no static state) so it can be unit-tested in isolation.
    /// </summary>
    public static class SyncJobCompletion
    {
        /// <summary>
        /// True when the caller has to end this job itself.
        /// </summary>
        /// <param name="status">
        /// The job's current status, or null when there is no job - the sync threw before one was
        /// started, or it has already been cleaned up. Nothing to close in either case.
        /// </param>
        public static bool NeedsClosing(SyncJobStatus? status) => status == SyncJobStatus.Running;
    }
}

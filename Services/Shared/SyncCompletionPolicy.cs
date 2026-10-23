namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Decides whether an account's sync run finished cleanly enough to record progress by
    /// advancing <c>MailAccount.LastSync</c>.
    ///
    /// This exists as its own type for one reason: the rule used to be an inline
    /// <c>failedEmails == 0</c> next to the code that writes <c>LastSync</c>, and a folder that
    /// could not be opened reached it disguised as a number of failed messages. Naming the rule
    /// keeps the two kinds of failure distinguishable at the point where the decision is actually
    /// made, and puts it somewhere a test can reach.
    ///
    /// The rule is deliberately unchanged from the previous behaviour: a folder-level failure still
    /// blocks <c>LastSync</c>, exactly as it did when it was being counted as failed messages.
    /// Whether an inaccessible folder <em>should</em> hold up the whole account is a separate
    /// question — letting it through would mean an account with one permanently unopenable folder
    /// starts advancing <c>LastSync</c> where it never did before, and a subsequent incremental
    /// sync would no longer re-examine the mail that folder never yielded. If that decision is ever
    /// taken, this is the one place to take it.
    /// </summary>
    public static class SyncCompletionPolicy
    {
        /// <summary>
        /// True when nothing failed during the run: no individual message and no folder as a whole.
        /// </summary>
        /// <param name="failedEmails">
        /// Messages that could not be archived, counted one per message. A message the
        /// <c>MessageNotFoundException</c> recovery pulled through is not one of these.
        /// </param>
        /// <param name="failedFolders">
        /// Folders that failed as a unit — the server reported them through LIST/LSUB but they
        /// could not be opened or searched. Counted one per folder, never converted into a number
        /// of messages.
        /// </param>
        public static bool MayAdvanceLastSync(int failedEmails, int failedFolders)
        {
            return failedEmails == 0 && failedFolders == 0;
        }
    }
}

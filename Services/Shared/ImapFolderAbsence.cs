using MailKit;
using MailKit.Net.Imap;

namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Tells a folder that is <em>gone</em> apart from a folder that <em>failed</em>.
    ///
    /// Folder discovery unions a recursive LIST, a per-level LIST and LSUB, and any of the three can
    /// name a mailbox the server then refuses to open. The most common reason is not a fault at all:
    /// RFC 3501 forbids a server to drop a name from the subscription list when the mailbox behind it
    /// is deleted, so a folder somebody once subscribed to in an IMAP client stays in LSUB forever.
    /// A mailbox deleted between the LIST and the EXAMINE lands here too.
    ///
    /// "NO ... doesn't exist" is an answer, not an error. There is nothing to retry, nothing to
    /// preserve and nothing to come back for, so it must not be counted as a failed folder — a
    /// counted failure holds LastSync back, and a folder that does not exist never starts existing
    /// again, which leaves the account stuck for good.
    ///
    /// Everything else stays a failure: no permission, connection loss, throttling, a server error.
    /// Those can succeed on the next run and the account should be held back until they do.
    ///
    /// Pure (no I/O, no static state) so it can be unit-tested in isolation.
    /// </summary>
    public static class ImapFolderAbsence
    {
        /// <summary>
        /// Message fragments used when the server offers no machine-readable code. Deliberately
        /// short and deliberately few: every entry here is a guess about wording, and a wrong guess
        /// silently downgrades a real failure into "not there".
        /// </summary>
        private static readonly string[] AbsenceWordings =
        {
            "doesn't exist",
            "does not exist",
            "no such mailbox",
            "nonexistent mailbox"
        };

        /// <summary>
        /// True when the server said the mailbox is not there.
        /// </summary>
        public static bool IsFolderGone(Exception? ex)
        {
            var current = ex;
            while (current != null)
            {
                // MailKit's own signal, where the code path produces it.
                if (current is FolderNotFoundException)
                    return true;

                if (current is ImapCommandException imapEx &&
                    imapEx.Response == ImapCommandResponse.No)
                {
                    var message = imapEx.Message ?? string.Empty;

                    // RFC 5530's response code, which says it without ambiguity. Exchange 2010
                    // predates it and sends prose instead, hence the wordings below.
                    if (message.IndexOf("[NONEXISTENT]", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;

                    foreach (var wording in AbsenceWordings)
                    {
                        if (message.IndexOf(wording, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                }

                current = current.InnerException;
            }

            return false;
        }
    }
}

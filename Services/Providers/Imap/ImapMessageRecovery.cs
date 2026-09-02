using MailKit;
using MailKit.Net.Imap;
using MimeKit;

namespace MailArchiver.Services.Providers.Imap
{
    /// <summary>
    /// One bounded second attempt at a message that <c>GetMessageAsync</c> reported as missing.
    ///
    /// Why this exists: on some legacy servers — reproduced on on-premises Exchange 2010 — a UID
    /// fetch through <c>GetMessageAsync</c> raises <see cref="MessageNotFoundException"/> while the
    /// very same message still yields a well-formed MIME document over a plain BODY[] fetch. The
    /// exception is not a statement about the mailbox, it is MailKit reporting that the response it
    /// correlated to the request did not contain the message it asked for.
    ///
    /// Why <c>GetStreamsAsync</c>: it is callback-driven. MailKit hands each stream to the callback
    /// as it arrives and never checks afterwards whether the requested message was among them, so
    /// it does not raise <see cref="MessageNotFoundException"/> at all — a message the server does
    /// not return simply never reaches the callback. That makes it structurally, not accidentally,
    /// a different path from the call that just failed.
    ///
    /// What this is not: a retry. It runs at most once per UID, issues exactly one fetch, and never
    /// reports success unless raw bytes actually arrived and parsed. Anything less stays a failure,
    /// so an unretrieved UID can never be mistaken for an archived one. Read-only throughout —
    /// no flag is set, nothing is marked seen, moved or deleted.
    ///
    /// The retrieval and the parsing are separate on purpose: the parsing half is pure and unit
    /// tested, and the retrieval half can be replaced without touching the sync pipeline.
    /// </summary>
    public static class ImapMessageRecovery
    {
        /// <summary>
        /// Attempts to retrieve and parse the message behind <paramref name="uid"/> after the
        /// normal fetch reported it as missing.
        /// </summary>
        /// <returns>The parsed message, or null when nothing usable came back.</returns>
        public static async Task<MimeMessage?> TryRecoverAsync(
            IMailFolder folder,
            UniqueId uid,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            // GetStreamsAsync is IMAP-specific. Every caller in the sync pipeline holds an
            // ImapFolder, but the parameter is IMailFolder, so this stays a check rather than a cast.
            if (folder is not IImapFolder imapFolder)
            {
                logger.LogDebug(
                    "No IMAP fallback available for UID {Uid} in folder {FolderName}: not an IMAP folder",
                    uid, folder.FullName);
                return null;
            }

            using var buffer = new MemoryStream();
            var received = false;

            try
            {
                await imapFolder.GetStreamsAsync(
                    new[] { uid },
                    async (callbackFolder, index, callbackUid, stream, token) =>
                    {
                        // One UID was asked for, so one stream is expected. Should a server echo
                        // more than one response, taking only the first keeps the buffer a single
                        // MIME document instead of two concatenated ones.
                        if (received)
                            return;

                        // MailKit disposes the stream as soon as this callback returns, so the
                        // bytes have to be taken now rather than queued for later.
                        received = true;
                        await stream.CopyToAsync(buffer, token);
                    },
                    cancellationToken,
                    null);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "IMAP fallback fetch failed for UID {Uid} in folder {FolderName}",
                    uid, folder.FullName);
                return null;
            }

            if (!received || buffer.Length == 0)
            {
                logger.LogDebug(
                    "IMAP fallback returned no data for UID {Uid} in folder {FolderName}",
                    uid, folder.FullName);
                return null;
            }

            buffer.Position = 0;
            var message = await ParseAsync(buffer, cancellationToken);

            if (message == null)
            {
                logger.LogWarning(
                    "IMAP fallback retrieved {Bytes} bytes for UID {Uid} in folder {FolderName} " +
                    "but they did not parse as a MIME message",
                    buffer.Length, uid, folder.FullName);
            }

            return message;
        }

        /// <summary>
        /// Parses raw bytes into a message. Returns null instead of throwing, because a fallback
        /// that cannot parse what it received has simply failed and the caller counts it as such.
        /// </summary>
        internal static async Task<MimeMessage?> ParseAsync(Stream raw, CancellationToken cancellationToken = default)
        {
            try
            {
                return await MimeMessage.LoadAsync(raw, cancellationToken);
            }
            catch (FormatException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
        }
    }
}

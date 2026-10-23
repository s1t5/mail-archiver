namespace MailArchiver.Services.Shared
{
    /// <summary>
    /// Turns a stored sync checkpoint into the UID a folder sync should resume after.
    ///
    /// The checkpoint used to remember the Date header of the last archived message, and the resume
    /// path fed that into the folder search. Two things were wrong with it: the search asks the server
    /// for INTERNALDATE, which is a different clock and on migrated mail can differ by years, and
    /// messages are processed in UID order rather than date order, so the last one processed is not
    /// the newest one processed. Either mismatch could move the search window past messages that had
    /// never been archived.
    ///
    /// A UID watermark has neither problem. The search runs exactly as it would without a checkpoint
    /// and the already-archived prefix is dropped from its result, so the window never moves and
    /// nothing can fall out of it.
    ///
    /// Pure (no I/O, no static state) so it can be unit-tested in isolation.
    /// </summary>
    public static class SyncResumePoint
    {
        /// <summary>
        /// The UID to resume after: everything above it still needs archiving. <c>0</c> means the
        /// checkpoint cannot be used and the folder has to be read in full — which is always safe,
        /// because the duplicate check absorbs whatever gets re-read.
        /// </summary>
        /// <param name="checkpointLastUid">UID stored on the checkpoint, if any.</param>
        /// <param name="checkpointUidValidity">UIDVALIDITY stored alongside it, if any.</param>
        /// <param name="folderUidValidity">The folder's current UIDVALIDITY.</param>
        public static uint ResolveAfterUid(long? checkpointLastUid, long? checkpointUidValidity, uint folderUidValidity)
        {
            // No watermark at all — checkpoints written before UIDs were recorded land here, as do
            // rows created by GetOrCreateCheckpointAsync before the first message was archived.
            if (checkpointLastUid is not > 0)
                return 0;

            // The server renumbered the folder, so the stored UID now names a different message (or
            // none). A checkpoint without a recorded UIDVALIDITY cannot be proven to match either.
            if (checkpointUidValidity is null || checkpointUidValidity != folderUidValidity)
                return 0;

            // IMAP UIDs are 32 bit. Anything wider is corruption, not a watermark.
            if (checkpointLastUid > uint.MaxValue)
                return 0;

            return (uint)checkpointLastUid.Value;
        }
    }
}

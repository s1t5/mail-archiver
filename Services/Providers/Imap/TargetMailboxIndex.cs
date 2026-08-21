// Services/Providers/Imap/TargetMailboxIndex.cs
using MailArchiver.Models;
using MailArchiver.Services.Shared;
using MailArchiver.Utilities;
using MailKit;
using MailKit.Net.Imap;

namespace MailArchiver.Services.Providers.Imap
{
    /// <summary>How a message was recognised as already present in the target mailbox.</summary>
    public enum OffloadMatchKind
    {
        None = 0,
        MessageId = 1,
        Fingerprint = 2,
    }

    /// <summary>
    /// An in-memory index of what a target mailbox already holds, so that an offload can be
    /// repeated without duplicating mail.
    /// <para>
    /// The scope is the whole target account rather than one resolved folder. That way the index
    /// still holds if users reorganise their mail between two runs, and it holds if a run is
    /// repeated with a different folder rename map.
    /// </para>
    /// <para>
    /// Only 64 bit hashes are stored, never the strings they came from. Two string sets at the
    /// configured maximum of 500,000 entries would cost a few hundred megabytes; the hashes cost
    /// single digit megabytes. A hash collision would cause one message to be skipped as already
    /// present, which is an acceptable trade at this probability.
    /// </para>
    /// </summary>
    public class TargetMailboxIndex
    {
        private readonly HashSet<long> _messageIdKeys = new();

        // The second criterion needs a range comparison on the send time, so its timestamps
        // cannot be folded into the key. Quantising into two second buckets is not equivalent:
        // two copies of the same message one second apart can land in different buckets, and
        // then the match silently never fires.
        private readonly Dictionary<long, List<DateTime>> _fingerprints = new();

        private readonly DateTimeHelper _dateTimeHelper;

        internal TargetMailboxIndex(DateTimeHelper dateTimeHelper)
        {
            _dateTimeHelper = dateTimeHelper;
        }

        /// <summary>Messages read into the index.</summary>
        public int IndexedMessages { get; private set; }

        /// <summary>Folders read into the index.</summary>
        public int IndexedFolders { get; private set; }

        /// <summary>
        /// True when the message cap was reached and the index therefore covers less than the
        /// whole mailbox. Duplicate detection is correspondingly narrower, and this is logged
        /// rather than passed over in silence.
        /// </summary>
        public bool ScopeReduced { get; private set; }

        /// <summary>
        /// Reads every folder of the target account and indexes it. ENVELOPE alone feeds both
        /// criteria, so one bulk fetch per folder is enough.
        /// </summary>
        /// <param name="restrictToFolder">
        /// When given, only this folder is indexed instead of the whole mailbox. Used as the
        /// fallback once the message cap has been hit.
        /// </param>
        public static async Task<TargetMailboxIndex> BuildAsync(
            ImapClient client,
            DateTimeHelper dateTimeHelper,
            ILogger logger,
            int prefetchMaxMessages,
            IMailFolder? restrictToFolder = null,
            CancellationToken cancellationToken = default)
        {
            var index = new TargetMailboxIndex(dateTimeHelper);

            var folders = new List<IMailFolder>();
            if (restrictToFolder != null)
            {
                folders.Add(restrictToFolder);
                index.ScopeReduced = true;
            }
            else
            {
                folders.AddRange(await EnumerateFoldersAsync(client, logger, cancellationToken));
            }

            foreach (var folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (index.IndexedMessages >= prefetchMaxMessages)
                {
                    index.ScopeReduced = true;
                    logger.LogWarning(
                        "Target mailbox index reached the configured limit of {Limit} messages after {Folders} folders. " +
                        "Duplicate detection no longer covers the whole mailbox, so a repeated offload may append mail " +
                        "that is already present in folders which were not indexed. Raise Offload:PrefetchMaxMessages to avoid this.",
                        prefetchMaxMessages, index.IndexedFolders);
                    break;
                }

                try
                {
                    await index.IndexFolderAsync(folder, logger, prefetchMaxMessages, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // A folder that cannot be opened must not abort the whole job, but it does
                    // narrow the scope, so say so.
                    index.ScopeReduced = true;
                    logger.LogWarning(ex,
                        "Could not index target folder '{Folder}'. Mail already present there will not be recognised, " +
                        "so a repeated offload may duplicate it.", folder.FullName);
                }
            }

            logger.LogInformation(
                "Target mailbox index built: {Messages} messages across {Folders} folders, " +
                "{MessageIdKeys} Message-ID keys, {Fingerprints} fingerprints, scope reduced: {Reduced}",
                index.IndexedMessages, index.IndexedFolders, index._messageIdKeys.Count,
                index._fingerprints.Count, index.ScopeReduced);

            return index;
        }

        private static async Task<List<IMailFolder>> EnumerateFoldersAsync(
            ImapClient client, ILogger logger, CancellationToken cancellationToken)
        {
            var result = new List<IMailFolder>();

            if (client.PersonalNamespaces == null || client.PersonalNamespaces.Count == 0)
            {
                logger.LogWarning("No personal namespaces reported by the target server; indexing INBOX only");
                result.Add(client.Inbox);
                return result;
            }

            // The same recursive enumeration ImapFolderService uses.
            var folders = await client.GetFoldersAsync(
                client.PersonalNamespaces[0], StatusItems.None, subscribedOnly: false, cancellationToken);

            result.AddRange(folders.Where(f => !f.Attributes.HasFlag(FolderAttributes.NonExistent)
                                            && !f.Attributes.HasFlag(FolderAttributes.NoSelect)));

            if (!result.Any(f => f.FullName.Equals(client.Inbox.FullName, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(client.Inbox);
            }

            return result;
        }

        private async Task IndexFolderAsync(
            IMailFolder folder, ILogger logger, int prefetchMaxMessages, CancellationToken cancellationToken)
        {
            if (!folder.IsOpen)
            {
                await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
            }

            IndexedFolders++;

            if (folder.Count == 0) return;

            // One bulk fetch per folder; ENVELOPE carries subject, from, to, date and Message-ID,
            // which is everything both criteria need. Same shape as the bulk fetch in
            // ImapMailSyncService.
            var summaries = await folder.FetchAsync(
                0, -1, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId, cancellationToken);

            foreach (var summary in summaries)
            {
                if (IndexedMessages >= prefetchMaxMessages) return;

                var envelope = summary.Envelope;
                if (envelope == null) continue;

                IndexedMessages++;

                var messageIdKey = OffloadMatchKey.MessageIdKey(envelope.MessageId);
                if (messageIdKey.HasValue)
                {
                    _messageIdKeys.Add(messageIdKey.Value);
                }

                AddFingerprint(
                    OffloadMatchKey.FingerprintKeyFromAddresses(
                        envelope.From?.Mailboxes.Select(m => m.Address),
                        envelope.To?.Mailboxes.Select(m => m.Address),
                        envelope.Subject),
                    ToStoredTimestamp(envelope.Date));
            }
        }

        /// <summary>
        /// Converts an envelope date into the form <c>ArchivedEmail.SentDate</c> is stored in.
        /// <para>
        /// This conversion is not optional. <c>SentDate</c> is display-local and naive, while an
        /// envelope date arrives from the wire as an absolute <see cref="DateTimeOffset"/>.
        /// Comparing the two directly makes the plus/minus two second window miss by the full
        /// timezone offset, so under any non-UTC display timezone the fingerprint criterion would
        /// never fire at all.
        /// </para>
        /// </summary>
        private DateTime ToStoredTimestamp(DateTimeOffset? envelopeDate)
            => envelopeDate.HasValue
                ? _dateTimeHelper.ConvertToDisplayTimeZone(envelopeDate.Value)
                : DateTime.MinValue;

        private void AddFingerprint(long key, DateTime timestamp)
        {
            if (_fingerprints.TryGetValue(key, out var timestamps))
            {
                timestamps.Add(timestamp);
            }
            else
            {
                _fingerprints[key] = new List<DateTime> { timestamp };
            }
        }

        /// <summary>
        /// Whether the target mailbox already holds this archived message, and by which
        /// criterion. The Message-ID is tried first because it is by far the more reliable of
        /// the two; the fingerprint exists for the rows whose stored Message-ID is unusable.
        /// </summary>
        public OffloadMatchKind Match(ArchivedEmail email)
        {
            var messageIdKey = OffloadMatchKey.MessageIdKey(email.MessageId);
            if (messageIdKey.HasValue && _messageIdKeys.Contains(messageIdKey.Value))
            {
                return OffloadMatchKind.MessageId;
            }

            var fingerprint = OffloadMatchKey.FingerprintKeyFromStored(email);
            if (_fingerprints.TryGetValue(fingerprint, out var timestamps))
            {
                foreach (var candidate in timestamps)
                {
                    if (OffloadMatchKey.WithinTolerance(candidate, email.SentDate))
                    {
                        return OffloadMatchKind.Fingerprint;
                    }
                }
            }

            return OffloadMatchKind.None;
        }

        /// <summary>
        /// Records a message as present. Called after every successful append, so that
        /// duplicates within a single run are caught as well, not only duplicates against an
        /// earlier one.
        /// </summary>
        public void Add(ArchivedEmail email)
        {
            var messageIdKey = OffloadMatchKey.MessageIdKey(email.MessageId);
            if (messageIdKey.HasValue)
            {
                _messageIdKeys.Add(messageIdKey.Value);
            }

            AddFingerprint(OffloadMatchKey.FingerprintKeyFromStored(email), email.SentDate);
            IndexedMessages++;
        }

        /// <summary>
        /// Adds an entry from raw values, without needing an <see cref="ArchivedEmail"/>. Lets
        /// the match logic be exercised against a hand-built index, since the IMAP fetch that
        /// normally fills it cannot be unit tested. Internal, reached from the test project
        /// through the InternalsVisibleTo already declared in MailArchiver.csproj.
        /// </summary>
        internal void AddRaw(string? messageId, string? from, string? to, string? subject, DateTime sentDate)
        {
            var key = OffloadMatchKey.MessageIdKey(messageId);
            if (key.HasValue) _messageIdKeys.Add(key.Value);
            AddFingerprint(OffloadMatchKey.FingerprintKeyFromStored(from, to, subject), sentDate);
            IndexedMessages++;
        }
    }
}

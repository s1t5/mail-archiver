using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using MailArchiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;


namespace MailArchiver.Services
{
    /// <summary>
    /// EF Core SaveChanges interceptor that deduplicates attachment payloads.
    ///
    /// For every newly added <see cref="EmailAttachment"/> the pending bytes are
    /// hashed (SHA-256) and stored content-addressed in the AttachmentContents
    /// table. Identical bytes are stored exactly once; the attachment only keeps a
    /// foreign key (<see cref="EmailAttachment.AttachmentContentId"/>) to the shared
    /// content row. This covers ALL write paths (IMAP, Graph, EML/MBOX import,
    /// truncated content saver, ...) without changing each individual call site.
    ///
    /// All attachments of a single SaveChanges are upserted in ONE database round-trip
    /// (via unnest arrays) instead of one statement per attachment. The upsert uses
    /// <c>INSERT ... ON CONFLICT ("Hash") DO NOTHING</c>; the unique index on Hash
    /// guarantees a single physical copy even under concurrent writers.
    ///
    /// There is intentionally NO reference counter: the authoritative cleanup of
    /// unreferenced content rows is the orphan garbage collection
    /// (<see cref="AttachmentDeduplicationBackgroundService"/> / DatabaseMaintenanceService),
    /// which is based on actual references and therefore robust against every deletion path.
    /// </summary>
    public class AttachmentDeduplicationInterceptor : SaveChangesInterceptor
    {
        private readonly ILogger<AttachmentDeduplicationInterceptor> _logger;

        public AttachmentDeduplicationInterceptor(ILogger<AttachmentDeduplicationInterceptor> logger)
        {
            _logger = logger;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (eventData.Context != null)
            {
                Deduplicate(eventData.Context);
            }
            return base.SavingChanges(eventData, result);
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.Context != null)
            {
                await DeduplicateAsync(eventData.Context, cancellationToken);
            }
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        // ------------------------------------------------------------------
        // Shared preparation: collect pending attachments + their hashes.
        // ------------------------------------------------------------------

        /// <summary>An attachment plus the SHA-256 (hex) of its pending bytes.</summary>
        private readonly record struct PendingAttachment(EmailAttachment Attachment, string Hash, byte[] Bytes);

        private static List<PendingAttachment> CollectPending(DbContext context)
        {
            var entries = context.ChangeTracker
                .Entries<EmailAttachment>()
                .Where(e => e.State == EntityState.Added)
                .ToList();

            var pending = new List<PendingAttachment>(entries.Count);
            foreach (var entry in entries)
            {
                var attachment = entry.Entity;

                // Already deduplicated (e.g. set explicitly) – skip.
                if (attachment.AttachmentContentId.HasValue)
                    continue;

                var bytes = attachment.PendingContent ?? attachment.LegacyContent;
                if (bytes == null)
                    continue;

                pending.Add(new PendingAttachment(attachment, ComputeSha256Hex(bytes), bytes));
            }
            return pending;
        }

        /// <summary>Distinct (hash → bytes/size) payloads to be upserted.</summary>
        private static (string[] hashes, byte[][] contents, long[] sizes) BuildBatchArrays(
            List<PendingAttachment> pending)
        {
            var distinct = new Dictionary<string, byte[]>();
            foreach (var p in pending)
                distinct[p.Hash] = p.Bytes;

            var hashes = new string[distinct.Count];
            var contents = new byte[distinct.Count][];
            var sizes = new long[distinct.Count];
            var i = 0;
            foreach (var kv in distinct)
            {
                hashes[i] = kv.Key;
                contents[i] = kv.Value;
                sizes[i] = kv.Value.LongLength;
                i++;
            }
            return (hashes, contents, sizes);
        }

        private static void Assign(List<PendingAttachment> pending, IReadOnlyDictionary<string, int> hashToId)
        {
            foreach (var p in pending)
            {
                if (hashToId.TryGetValue(p.Hash, out var id))
                {
                    p.Attachment.AttachmentContentId = id;
                    p.Attachment.AttachmentContent = null; // avoid EF inserting a duplicate content row
                    p.Attachment.LegacyContent = null;     // never store bytes inline for new rows
                    p.Attachment.ClearPendingContent();
                }
            }
        }

        // SQL upsert for the whole batch in a single round-trip. The `ins` CTE returns
        // the rows newly inserted by this statement; the second SELECT returns the
        // already-existing rows (pre-snapshot) for hashes that hit ON CONFLICT DO NOTHING.
        // The UNION of both covers every hash exactly once.
        private const string BatchUpsertSql = @"
            WITH input AS (
                SELECT unnest(@hashes) AS h,
                       unnest(@contents) AS c,
                       unnest(@sizes) AS s
            ),
            ins AS (
                INSERT INTO mail_archiver.""AttachmentContents"" (""Hash"", ""Content"", ""Size"", ""CreatedAt"")
                SELECT DISTINCT ON (h) h, c, s, now() FROM input
                ON CONFLICT (""Hash"") DO NOTHING
                RETURNING ""Id"", ""Hash""
            )
            SELECT ""Id"", ""Hash"" FROM ins
            UNION
            SELECT ""Id"", ""Hash"" FROM mail_archiver.""AttachmentContents"" WHERE ""Hash"" = ANY(@hashes);";

        // ------------------------------------------------------------------
        // Async path
        // ------------------------------------------------------------------
        private async Task DeduplicateAsync(DbContext context, CancellationToken cancellationToken)
        {
            var pending = CollectPending(context);
            if (pending.Count == 0)
                return;

            var (hashes, contents, sizes) = BuildBatchArrays(pending);

            var connection = context.Database.GetDbConnection();
            var transaction = context.Database.CurrentTransaction?.GetDbTransaction();

            // EF usually has not opened the connection yet at interception time, so we
            // open it ourselves and only close what we opened (openedHere). NOTE: when
            // there is no ambient transaction the content rows are committed independently
            // of the subsequent EF save; should that save fail, the (now unreferenced)
            // content rows are reclaimed by the authoritative orphan garbage collection.
            var openedHere = false;
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
                openedHere = true;
            }

            try
            {
                var hashToId = new Dictionary<string, int>(hashes.Length);
                using var command = connection.CreateCommand();
                if (transaction != null)
                    command.Transaction = transaction;
                command.CommandText = BatchUpsertSql;
                AddArrayParameters(command, hashes, contents, sizes);

                await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                        hashToId[reader.GetString(1)] = reader.GetInt32(0);
                }

                Assign(pending, hashToId);
            }
            finally
            {
                if (openedHere && connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }
        }

        // ------------------------------------------------------------------
        // Sync path (true synchronous ADO.NET, no sync-over-async)
        // ------------------------------------------------------------------
        private void Deduplicate(DbContext context)
        {
            var pending = CollectPending(context);
            if (pending.Count == 0)
                return;

            var (hashes, contents, sizes) = BuildBatchArrays(pending);

            var connection = context.Database.GetDbConnection();
            var transaction = context.Database.CurrentTransaction?.GetDbTransaction();

            var openedHere = false;
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
                openedHere = true;
            }

            try
            {
                var hashToId = new Dictionary<string, int>(hashes.Length);
                using var command = connection.CreateCommand();
                if (transaction != null)
                    command.Transaction = transaction;
                command.CommandText = BatchUpsertSql;
                AddArrayParameters(command, hashes, contents, sizes);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                        hashToId[reader.GetString(1)] = reader.GetInt32(0);
                }

                Assign(pending, hashToId);
            }
            finally
            {
                if (openedHere && connection.State == ConnectionState.Open)
                    connection.Close();
            }
        }

        private static string ComputeSha256Hex(byte[] bytes)
        {
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static void AddArrayParameters(DbCommand command, string[] hashes, byte[][] contents, long[] sizes)
        {
            AddParameter(command, "@hashes", hashes);
            AddParameter(command, "@contents", contents);
            AddParameter(command, "@sizes", sizes);
        }

        private static void AddParameter(DbCommand command, string name, object value)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            command.Parameters.Add(p);
        }
    }
}

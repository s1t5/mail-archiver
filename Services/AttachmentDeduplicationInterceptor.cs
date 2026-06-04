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
    /// The upsert uses <c>INSERT ... ON CONFLICT ("Hash") DO UPDATE</c> so it is
    /// safe under concurrent writers (the unique index on Hash guarantees a single
    /// physical copy).
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
                DeduplicateAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
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

        private async Task DeduplicateAsync(DbContext context, CancellationToken cancellationToken)
        {
            var entries = context.ChangeTracker
                .Entries<EmailAttachment>()
                .Where(e => e.State == EntityState.Added)
                .ToList();

            if (entries.Count == 0)
                return;

            var connection = context.Database.GetDbConnection();
            var transaction = context.Database.CurrentTransaction?.GetDbTransaction();

            var openedHere = false;
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
                openedHere = true;
            }

            try
            {
                foreach (var entry in entries)
                {
                    var attachment = entry.Entity;

                    // Already deduplicated (e.g. set explicitly) – skip.
                    if (attachment.AttachmentContentId.HasValue)
                        continue;

                    var bytes = attachment.PendingContent ?? attachment.LegacyContent;
                    if (bytes == null)
                        continue;

                    var hash = ComputeSha256Hex(bytes);
                    var contentId = await UpsertContentAsync(
                        connection, transaction, hash, bytes, cancellationToken);

                    attachment.AttachmentContentId = contentId;
                    attachment.AttachmentContent = null; // avoid EF inserting a duplicate content row
                    attachment.LegacyContent = null;     // never store bytes inline for new rows
                    attachment.ClearPendingContent();
                }
            }
            finally
            {
                if (openedHere && connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private static string ComputeSha256Hex(byte[] bytes)
        {
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private async Task<int> UpsertContentAsync(
            DbConnection connection, DbTransaction? transaction,
            string hash, byte[] bytes, CancellationToken cancellationToken)
        {
            using var command = connection.CreateCommand();
            if (transaction != null)
                command.Transaction = transaction;

            command.CommandText = @"
                INSERT INTO mail_archiver.""AttachmentContents"" (""Hash"", ""Content"", ""Size"", ""ReferenceCount"", ""CreatedAt"")
                VALUES (@hash, @content, @size, 1, now())
                ON CONFLICT (""Hash"") DO UPDATE
                    SET ""ReferenceCount"" = mail_archiver.""AttachmentContents"".""ReferenceCount"" + 1
                RETURNING ""Id"";";

            AddParameter(command, "@hash", hash);
            AddParameter(command, "@content", bytes);
            AddParameter(command, "@size", (long)bytes.LongLength);

            var idObj = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(idObj);
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

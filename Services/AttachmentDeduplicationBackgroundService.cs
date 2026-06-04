using Npgsql;

namespace MailArchiver.Services
{
    /// <summary>
    /// Resumable background migration that deduplicates the attachment payloads of
    /// EXISTING data (attachments archived before the dedup feature was introduced).
    ///
    /// It processes the legacy inline <c>Content</c> column of the EmailAttachments
    /// table batch by batch, moves every unique payload into the content-addressed
    /// AttachmentContents table (hashing done in PostgreSQL via pgcrypto), points the
    /// attachment to the shared content row and finally NULLs the legacy column.
    ///
    /// Robustness / restart-safety:
    ///   * Each batch runs in its own transaction.
    ///   * Progress is persisted in mail_archiver.AttachmentDeduplicationState
    ///     (a cursor over EmailAttachments.Id), so an application restart simply
    ///     continues where it left off.
    ///   * The work is idempotent: already migrated rows (AttachmentContentId IS NOT
    ///     NULL or Content IS NULL) are skipped, so even with a lost/zeroed cursor the
    ///     migration never corrupts or double-processes data.
    ///   * New attachments written while this runs are already deduplicated by the
    ///     AttachmentDeduplicationInterceptor, so they are not touched here.
    /// </summary>
    public class AttachmentDeduplicationBackgroundService : BackgroundService
    {
        private readonly ILogger<AttachmentDeduplicationBackgroundService> _logger;
        private readonly IConfiguration _configuration;
        private readonly ISyncJobService _syncJobService;

        public AttachmentDeduplicationBackgroundService(
            ILogger<AttachmentDeduplicationBackgroundService> logger,
            IConfiguration configuration,
            ISyncJobService syncJobService)
        {
            _logger = logger;
            _configuration = configuration;
            _syncJobService = syncJobService;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Attachment deduplication is a core feature and therefore ALWAYS on.
            // Only its batch/scheduling parameters are configurable.
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError("Attachment Deduplication: connection string not found");
                return;
            }

            var batchSize = _configuration.GetValue<int>("AttachmentDeduplication:BatchSize", 200);
            if (batchSize < 1) batchSize = 200;
            var delayMs = _configuration.GetValue<int>("AttachmentDeduplication:DelayBetweenBatchesMs", 0);
            var startupDelaySeconds = _configuration.GetValue<int>("AttachmentDeduplication:StartupDelaySeconds", 20);
            var cleanupIntervalHours = _configuration.GetValue<int>("AttachmentDeduplication:OrphanCleanupIntervalHours", 12);
            if (cleanupIntervalHours < 1) cleanupIntervalHours = 12;


            // Give the schema migration (and the rest of the app startup) time to finish.
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(startupDelaySeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // 1) One-time (resumable) migration of existing inline payloads.
            try
            {
                await RunMigrationAsync(connectionString, batchSize, delayMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Attachment Deduplication migration cancelled; will resume on next start");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Attachment Deduplication migration failed: {Message}. " +
                    "It will resume on the next application start.", ex.Message);
            }

            // 2) Periodic orphan garbage collection + the one-time space reclamation.
            //    Both ALWAYS run (independent of the DatabaseMaintenance feature).
            //    The reclamation (VACUUM FULL) is only allowed while NO sync jobs are
            //    running; until it has succeeded we poll on a short interval so it kicks
            //    in as soon as the syncs are idle, then fall back to the normal cleanup
            //    interval.
            var reclaimPending = true;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupOrphansAsync(connectionString, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Attachment Deduplication orphan cleanup failed: {Message}", ex.Message);
                }

                if (reclaimPending)
                {
                    try
                    {
                        reclaimPending = !await TryReclaimSpaceAsync(connectionString, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Attachment Deduplication space reclamation failed: {Message}; will retry", ex.Message);
                    }
                }

                // While the one-time reclamation is still pending (e.g. deferred because
                // sync jobs are running) poll frequently; afterwards use the normal interval.
                var delay = reclaimPending ? TimeSpan.FromMinutes(5) : TimeSpan.FromHours(cleanupIntervalHours);
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }


        /// <summary>
        /// Deletes AttachmentContents rows that are no longer referenced by any
        /// EmailAttachment. Authoritative GC based on actual references (robust against
        /// every deletion path). Runs always, regardless of the DatabaseMaintenance setting.
        /// </summary>
        private async Task CleanupOrphansAsync(string connectionString, CancellationToken token)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(token);

            if (!await TableExistsAsync(connection, "AttachmentContents", token))
                return;

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM mail_archiver.""AttachmentContents"" ac
                WHERE NOT EXISTS (
                    SELECT 1 FROM mail_archiver.""EmailAttachments"" e
                    WHERE e.""AttachmentContentId"" = ac.""Id""
                );";
            var deleted = await command.ExecuteNonQueryAsync(token);
            if (deleted > 0)
            {
                _logger.LogInformation("Attachment Deduplication orphan cleanup removed {Count} unreferenced content row(s)", deleted);
            }
        }


        private async Task RunMigrationAsync(string connectionString, int batchSize, int delayMs, CancellationToken token)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(token);

            // The state table is created by the EF migration; bail out gracefully if it is missing.
            if (!await TableExistsAsync(connection, "AttachmentDeduplicationState", token))
            {
                _logger.LogWarning("Attachment Deduplication migration: state table not found (schema migration not applied yet); skipping");
                return;
            }

            await EnsureStateRowAsync(connection, token);

            var (isCompleted, cursor) = await ReadStateAsync(connection, token);
            if (isCompleted)
            {
                // Verify the "completed" flag against reality: only trust it when no legacy
                // (un-deduplicated) attachments remain. This self-heals databases that were
                // flagged complete by an earlier buggy run.
                var remaining = await CountRemainingLegacyAsync(connection, token);
                if (remaining == 0)
                {
                    _logger.LogInformation("Attachment Deduplication migration already completed; nothing to do");
                    // The one-time space reclamation (VACUUM FULL) is handled by the
                    // periodic loop in ExecuteAsync (gated on no running sync jobs); the
                    // ReclaimedAt marker makes it a no-op once it has already run.
                    return;
                }



                _logger.LogWarning("Attachment Deduplication was flagged completed but {Remaining} legacy attachment(s) remain; resuming from start",
                    remaining);
                cursor = 0;
                await ResetCompletedAsync(connection, token);
            }


            _logger.LogInformation("Attachment Deduplication migration starting at cursor Id > {Cursor} (batch size {BatchSize})",
                cursor, batchSize);

            long totalProcessed = 0;
            var startTime = DateTime.UtcNow;

            while (!token.IsCancellationRequested)
            {
                var (newCursor, processed) = await ProcessBatchAsync(connection, cursor, batchSize, token);

                if (processed == 0)
                    break;

                cursor = newCursor;
                totalProcessed += processed;

                if (totalProcessed % (batchSize * 25L) < batchSize)
                {
                    _logger.LogInformation("Attachment Deduplication migration progress: {Total} attachments processed (cursor {Cursor})",
                        totalProcessed, cursor);
                }

                if (delayMs > 0)
                    await Task.Delay(delayMs, token);
            }

            if (!token.IsCancellationRequested)
            {
                await MarkCompletedAsync(connection, token);
                var duration = DateTime.UtcNow - startTime;
                _logger.LogInformation("Attachment Deduplication migration COMPLETED. {Total} attachments processed in {Minutes:F1} minutes.",
                    totalProcessed, duration.TotalMinutes);

                // The physical space reclamation (one-time VACUUM FULL) is performed by the
                // periodic loop in ExecuteAsync, gated on no running sync jobs.
            }
        }

        /// <summary>
        /// One-time, restart-safe physical space reclamation after the data migration.
        ///
        /// The migration copies every payload into AttachmentContents and sets the legacy
        /// EmailAttachments.Content to NULL. Due to PostgreSQL MVCC the old bytea values
        /// remain as dead tuples in the EmailAttachments TOAST table; a normal VACUUM does
        /// not return that space to the OS. We therefore run a single
        /// <c>VACUUM FULL mail_archiver."EmailAttachments"</c> (which rewrites the heap and
        /// its TOAST table) so the deduplication actually lowers the on-disk size.
        ///
        /// Guarded by the AttachmentDeduplicationState.ReclaimedAt marker so it runs exactly
        /// once, even across application restarts/crashes.
        ///
        /// It is ONLY allowed to run while NO sync jobs are active, because VACUUM FULL takes
        /// an ACCESS EXCLUSIVE lock on EmailAttachments (which would block / be blocked by a
        /// running synchronization that writes attachments). It must also NOT run inside a
        /// transaction block (none is active on the dedicated connection used here).
        /// </summary>
        /// <returns>
        /// <c>true</c> when nothing more needs to be done (already reclaimed, no state table,
        /// or the VACUUM just completed); <c>false</c> when it was deferred because sync jobs
        /// are currently running and it should be retried later.
        /// </returns>
        private async Task<bool> TryReclaimSpaceAsync(string connectionString, CancellationToken token)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(token);

            // State table missing (schema migration not applied) -> nothing to reclaim.
            if (!await TableExistsAsync(connection, "AttachmentDeduplicationState", token))
                return true;

            // Already reclaimed? -> done, stop polling.
            await using (var checkCmd = connection.CreateCommand())
            {
                checkCmd.CommandText = @"
                    SELECT ""ReclaimedAt"" IS NOT NULL
                    FROM mail_archiver.""AttachmentDeduplicationState""
                    WHERE ""Id"" = 1;";
                var alreadyDone = await checkCmd.ExecuteScalarAsync(token);
                if (alreadyDone != null && alreadyDone != DBNull.Value && (bool)alreadyDone)
                    return true;
            }

            // Only reclaim once the data migration is actually finished. If it is not
            // completed yet (or still has legacy rows) the migration is still writing, so defer.

            var (isCompleted, _) = await ReadStateAsync(connection, token);
            if (!isCompleted || await CountRemainingLegacyAsync(connection, token) > 0)
                return false;

            // Never run VACUUM FULL while a synchronization is in progress: it would take an
            // exclusive lock on EmailAttachments and block/conflict with the sync writes.
            var activeSyncJobs = _syncJobService.GetActiveJobs().Count;
            if (activeSyncJobs > 0)
            {
                _logger.LogInformation("Attachment Deduplication: deferring one-time VACUUM FULL because {Count} sync job(s) are running; will retry later",
                    activeSyncJobs);
                return false;
            }

            _logger.LogWarning("Attachment Deduplication: starting one-time VACUUM FULL on EmailAttachments to reclaim freed disk space. " +
                "This takes an exclusive lock on the table and may run for a while on large databases.");

            var startTime = DateTime.UtcNow;
            await using (var vacuumCmd = connection.CreateCommand())
            {
                // 0 = no timeout; VACUUM FULL can take a long time on big tables.
                vacuumCmd.CommandTimeout = 0;
                vacuumCmd.CommandText = @"VACUUM (FULL, ANALYZE) mail_archiver.""EmailAttachments"";";
                await vacuumCmd.ExecuteNonQueryAsync(token);
            }
            var duration = DateTime.UtcNow - startTime;

            await using (var markCmd = connection.CreateCommand())
            {
                markCmd.CommandText = @"
                    UPDATE mail_archiver.""AttachmentDeduplicationState""
                    SET ""ReclaimedAt"" = now(),
                        ""UpdatedAt"" = now()
                    WHERE ""Id"" = 1;";
                await markCmd.ExecuteNonQueryAsync(token);
            }

            _logger.LogInformation("Attachment Deduplication: VACUUM FULL on EmailAttachments completed in {Minutes:F1} minutes; disk space reclaimed.",
                duration.TotalMinutes);
            return true;
        }



        /// <summary>
        /// Processes a single batch in its own transaction and persists the new cursor.
        /// Returns the new cursor and the number of attachments migrated in this batch.
        /// </summary>
        private async Task<(long cursor, int processed)> ProcessBatchAsync(
            NpgsqlConnection connection, long cursor, int batchSize, CancellationToken token)
        {
            await using var tx = await connection.BeginTransactionAsync(token);

            int processed;
            long newCursor;

            // Step 1: insert the unique payloads of this batch into the content-addressed
            // table. This MUST be a separate statement from the UPDATE below: a single
            // multi-CTE statement runs all data-modifying CTEs against the same snapshot,
            // so the UPDATE would not see rows just inserted by the INSERT CTE and would
            // therefore match 0 rows for any not-yet-seen hash.
            await using (var insCmd = connection.CreateCommand())
            {
                insCmd.Transaction = tx;
                insCmd.CommandText = @"
                    WITH batch AS (
                        SELECT ""Id"", ""Content""
                        FROM mail_archiver.""EmailAttachments""
                        WHERE ""Id"" > @cursor
                          AND ""AttachmentContentId"" IS NULL
                          AND ""Content"" IS NOT NULL
                        ORDER BY ""Id""
                        LIMIT @batch
                    ),
                    hashed AS (
                        SELECT encode(digest(""Content"", 'sha256'), 'hex') AS h,
                               ""Content"" AS c,
                               octet_length(""Content"")::bigint AS sz
                        FROM batch
                    )
                    INSERT INTO mail_archiver.""AttachmentContents"" (""Hash"", ""Content"", ""Size"", ""CreatedAt"")
                    SELECT DISTINCT ON (h) h, c, sz, now()
                    FROM hashed

                    ORDER BY h
                    ON CONFLICT (""Hash"") DO NOTHING;";
                insCmd.Parameters.AddWithValue("@cursor", cursor);
                insCmd.Parameters.AddWithValue("@batch", batchSize);
                await insCmd.ExecuteNonQueryAsync(token);
            }

            // Step 2: point the batch's attachments at the (now visible) content rows and
            // free the legacy inline payload. Returns the new cursor + processed count.
            await using (var updCmd = connection.CreateCommand())
            {
                updCmd.Transaction = tx;
                updCmd.CommandText = @"
                    WITH batch AS (
                        SELECT ""Id"", ""Content""
                        FROM mail_archiver.""EmailAttachments""
                        WHERE ""Id"" > @cursor
                          AND ""AttachmentContentId"" IS NULL
                          AND ""Content"" IS NOT NULL
                        ORDER BY ""Id""
                        LIMIT @batch
                    ),
                    hashed AS (
                        SELECT ""Id"",
                               encode(digest(""Content"", 'sha256'), 'hex') AS h
                        FROM batch
                    ),
                    upd AS (
                        UPDATE mail_archiver.""EmailAttachments"" e
                        SET ""AttachmentContentId"" = ac.""Id"",
                            ""Content"" = NULL
                        FROM hashed hsh
                        JOIN mail_archiver.""AttachmentContents"" ac ON ac.""Hash"" = hsh.h
                        WHERE e.""Id"" = hsh.""Id""
                        RETURNING e.""Id""
                    )
                    SELECT COALESCE(MAX(""Id""), @cursor)::bigint AS new_cursor, COUNT(*)::int AS processed
                    FROM upd;";
                updCmd.Parameters.AddWithValue("@cursor", cursor);
                updCmd.Parameters.AddWithValue("@batch", batchSize);

                await using var reader = await updCmd.ExecuteReaderAsync(token);
                await reader.ReadAsync(token);
                newCursor = reader.GetInt64(0);
                processed = reader.GetInt32(1);
            }


            if (processed > 0)
            {
                await using var stateCmd = connection.CreateCommand();
                stateCmd.Transaction = tx;
                stateCmd.CommandText = @"
                    UPDATE mail_archiver.""AttachmentDeduplicationState""
                    SET ""LastProcessedId"" = @cursor,
                        ""ProcessedCount"" = ""ProcessedCount"" + @processed,
                        ""UpdatedAt"" = now()
                    WHERE ""Id"" = 1;";
                stateCmd.Parameters.AddWithValue("@cursor", newCursor);
                stateCmd.Parameters.AddWithValue("@processed", (long)processed);
                await stateCmd.ExecuteNonQueryAsync(token);
            }

            await tx.CommitAsync(token);
            return (newCursor, processed);
        }

        private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string table, CancellationToken token)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'mail_archiver' AND table_name = @table
                );";
            command.Parameters.AddWithValue("@table", table);
            var result = await command.ExecuteScalarAsync(token);
            return result != null && (bool)result;
        }

        private static async Task EnsureStateRowAsync(NpgsqlConnection connection, CancellationToken token)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO mail_archiver.""AttachmentDeduplicationState"" (""Id"", ""LastProcessedId"", ""ProcessedCount"", ""IsCompleted"", ""StartedAt"", ""UpdatedAt"")
                VALUES (1, 0, 0, false, now(), now())
                ON CONFLICT (""Id"") DO NOTHING;";
            await command.ExecuteNonQueryAsync(token);
        }

        private static async Task<(bool isCompleted, long cursor)> ReadStateAsync(NpgsqlConnection connection, CancellationToken token)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT ""IsCompleted"", ""LastProcessedId""
                FROM mail_archiver.""AttachmentDeduplicationState""
                WHERE ""Id"" = 1;";
            await using var reader = await command.ExecuteReaderAsync(token);
            if (await reader.ReadAsync(token))
            {
                return (reader.GetBoolean(0), reader.GetInt64(1));
            }
            return (false, 0);
        }

        private static async Task<long> CountRemainingLegacyAsync(NpgsqlConnection connection, CancellationToken token)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT count(*)::bigint
                FROM mail_archiver.""EmailAttachments""
                WHERE ""AttachmentContentId"" IS NULL
                  AND ""Content"" IS NOT NULL;";
            var result = await command.ExecuteScalarAsync(token);
            return result == null ? 0 : Convert.ToInt64(result);
        }

        private static async Task ResetCompletedAsync(NpgsqlConnection connection, CancellationToken token)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE mail_archiver.""AttachmentDeduplicationState""
                SET ""IsCompleted"" = false,
                    ""LastProcessedId"" = 0,
                    ""CompletedAt"" = NULL,
                    ""UpdatedAt"" = now()
                WHERE ""Id"" = 1;";
            await command.ExecuteNonQueryAsync(token);
        }

        private static async Task MarkCompletedAsync(NpgsqlConnection connection, CancellationToken token)

        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE mail_archiver.""AttachmentDeduplicationState""
                SET ""IsCompleted"" = true,
                    ""CompletedAt"" = now(),
                    ""UpdatedAt"" = now()
                WHERE ""Id"" = 1;";
            await command.ExecuteNonQueryAsync(token);
        }
    }
}

namespace MailArchiver.Data
{
    /// <summary>
    /// Trigram GIN index backing substring search (<c>*term*</c>). The index expression must match
    /// the LIKE expression built in <c>EmailCoreService.SearchEmailsOptimizedAsync</c>.
    /// </summary>
    internal static class TrgmSearchIndex
    {
        public const string IndexName = "idx_archivedemails_trgm_search";

        /// <summary>
        /// Idempotent: creates the index when pg_trgm is installed and the index is missing,
        /// otherwise a no-op. Used on startup because the migration creates the index only
        /// best-effort (pg_trgm may be installed later by a superuser).
        /// </summary>
        public const string EnsureSql = @"
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm')
                   AND NOT EXISTS (SELECT 1 FROM pg_indexes
                                   WHERE schemaname = 'mail_archiver'
                                     AND tablename  = 'ArchivedEmails'
                                     AND indexname  = 'idx_archivedemails_trgm_search') THEN
                    CREATE INDEX ""idx_archivedemails_trgm_search""
                    ON mail_archiver.""ArchivedEmails""
                    USING GIN (lower(
                        COALESCE(""Subject"", '') || ' ' ||
                        COALESCE(""Body"", '') || ' ' ||
                        COALESCE(""From"", '') || ' ' ||
                        COALESCE(""To"", '') || ' ' ||
                        COALESCE(""Cc"", '') || ' ' ||
                        COALESCE(""Bcc"", '')) gin_trgm_ops);
                    RAISE NOTICE 'trigram GIN index created (pg_trgm was installed after the initial migration)';
                END IF;
            END $$;";
    }
}

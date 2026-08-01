using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2607_3_TrgmSubstringSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Trigram (pg_trgm) GIN index enabling fast substring search (*term*) over the
            // concatenated e-mail fields. The index expression MUST match the LIKE expression
            // built in EmailCoreService.SearchEmailsOptimizedAsync for the planner to use it.
            // pg_trgm creation is best-effort: if it cannot be created for ANY reason (missing
            // privilege, or the extension files are not installed on the server), the index is
            // skipped with a NOTICE and substring search still works (via a slower scan).
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm') THEN
                        BEGIN
                            CREATE EXTENSION IF NOT EXISTS pg_trgm;
                        EXCEPTION WHEN OTHERS THEN
                            -- Best-effort: ANY failure to provide pg_trgm must NOT abort the
                            -- upgrade. Besides insufficient_privilege this also covers installs
                            -- where the extension files are absent (feature_not_supported /
                            -- undefined_file). Substring search still works via a slower
                            -- sequential scan; a superuser can later run: CREATE EXTENSION pg_trgm;
                            RAISE NOTICE 'pg_trgm unavailable (%), substring-search index skipped. A superuser can run: CREATE EXTENSION pg_trgm;', SQLERRM;
                        END;
                    END IF;

                    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm')
                       AND NOT EXISTS (SELECT 1 FROM pg_indexes
                                       WHERE schemaname = 'mail_archiver'
                                         AND tablename = 'ArchivedEmails'
                                         AND indexname = 'idx_archivedemails_trgm_search') THEN
                        CREATE INDEX ""idx_archivedemails_trgm_search""
                        ON mail_archiver.""ArchivedEmails""
                        USING GIN (lower(
                            COALESCE(""Subject"", '') || ' ' ||
                            COALESCE(""Body"", '') || ' ' ||
                            COALESCE(""From"", '') || ' ' ||
                            COALESCE(""To"", '') || ' ' ||
                            COALESCE(""Cc"", '') || ' ' ||
                            COALESCE(""Bcc"", '')) gin_trgm_ops);
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS mail_archiver.""idx_archivedemails_trgm_search"";");
        }
    }
}

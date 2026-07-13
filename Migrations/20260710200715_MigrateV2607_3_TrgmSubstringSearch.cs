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
            // pg_trgm creation is best-effort: if the app role lacks privilege, the index is
            // skipped with a NOTICE and substring search still works (via a slower scan).
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm') THEN
                        BEGIN
                            CREATE EXTENSION IF NOT EXISTS pg_trgm;
                        EXCEPTION WHEN insufficient_privilege THEN
                            RAISE NOTICE 'pg_trgm missing and could not be created (insufficient privilege). Substring-search index skipped. A superuser can run: CREATE EXTENSION pg_trgm;';
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

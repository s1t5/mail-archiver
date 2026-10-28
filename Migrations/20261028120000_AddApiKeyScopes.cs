using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <summary>
    /// Adds the scope list to API keys. The REST API used to be read-only, so
    /// every key that already exists is read-only by definition and is
    /// backfilled with 'read'. Only keys created afterwards can carry 'write'.
    /// </summary>
    public partial class AddApiKeyScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'ApiKeys'
                          AND column_name = 'Scopes'
                    ) THEN
                        ALTER TABLE mail_archiver.""ApiKeys""
                            ADD COLUMN ""Scopes"" varchar(64) NOT NULL DEFAULT 'read';
                    END IF;
                END $$;
            ");

            // Existing keys predate the write API and must stay read-only.
            migrationBuilder.Sql(@"
                UPDATE mail_archiver.""ApiKeys""
                SET ""Scopes"" = 'read'
                WHERE ""Scopes"" IS NULL OR ""Scopes"" = '';
            ");

            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""ApiKeys"".""Scopes""
                IS 'Comma-separated API key scopes: read, write. Default read.';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE mail_archiver.""ApiKeys"" DROP COLUMN IF EXISTS ""Scopes"";
            ");
        }
    }
}

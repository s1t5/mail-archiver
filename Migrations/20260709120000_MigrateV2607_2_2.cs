using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2607_2_2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // Display name preservation for From/To/Cc/Bcc
            // ============================================================
            // Adds nullable text columns to store the original display names
            // (e.g. "Max Mustermann") alongside the existing address-only fields.
            // The existing From/To/Cc/Bcc columns are unchanged and continue to
            // hold bare email addresses used for dedup, outgoing detection, FTS
            // search, and sorting. The new columns are only used by restore and
            // export to faithfully reconstruct the original address headers.
            // null = no display name was present in the source message.

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'ArchivedEmails'
                          AND column_name = 'FromDisplayName'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails""
                        ADD COLUMN ""FromDisplayName"" text NULL;
                        COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""FromDisplayName""
                            IS 'Original display name(s) of the From sender(s), comma-separated; null when no display name was present. Used for faithful restore/export.';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'ArchivedEmails'
                          AND column_name = 'ToDisplayNames'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails""
                        ADD COLUMN ""ToDisplayNames"" text NULL;
                        COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""ToDisplayNames""
                            IS 'Original display name(s) of the To recipient(s), comma-separated; null when no display name was present. Used for faithful restore/export.';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'ArchivedEmails'
                          AND column_name = 'CcDisplayNames'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails""
                        ADD COLUMN ""CcDisplayNames"" text NULL;
                        COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""CcDisplayNames""
                            IS 'Original display name(s) of the Cc recipient(s), comma-separated; null when no display name was present. Used for faithful restore/export.';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'ArchivedEmails'
                          AND column_name = 'BccDisplayNames'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails""
                        ADD COLUMN ""BccDisplayNames"" text NULL;
                        COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""BccDisplayNames""
                            IS 'Original display name(s) of the Bcc recipient(s), comma-separated; null when no display name was present. Used for faithful restore/export.';
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DROP COLUMN IF EXISTS also removes the column comments automatically.
            migrationBuilder.Sql(@"
                ALTER TABLE mail_archiver.""ArchivedEmails""
                DROP COLUMN IF EXISTS ""FromDisplayName"",
                DROP COLUMN IF EXISTS ""ToDisplayNames"",
                DROP COLUMN IF EXISTS ""CcDisplayNames"",
                DROP COLUMN IF EXISTS ""BccDisplayNames"";
            ");
        }
    }
}
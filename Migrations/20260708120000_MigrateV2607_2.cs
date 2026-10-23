using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2607_2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // Per-account sync scheduling
            // ============================================================
            // Allows overriding the global MailSync:IntervalMinutes per account.
            // When NULL the global default from appsettings.json is used.
            // FullSyncIntervalHours (nullable) enables an automatic full
            // resync on a schedule; when NULL no automatic full sync runs and
            // only the manual resync button is available. LastFullSync is the
            // watermark tracking when the last automatic full sync completed.

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'MailAccounts'
                          AND column_name = 'SyncIntervalMinutes'
                    ) THEN
                        ALTER TABLE mail_archiver.""MailAccounts""
                        ADD COLUMN ""SyncIntervalMinutes"" integer NULL;
                        COMMENT ON COLUMN mail_archiver.""MailAccounts"".""SyncIntervalMinutes""
                            IS 'Per-account sync interval in minutes; NULL uses the global MailSync:IntervalMinutes default from appsettings.json';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'MailAccounts'
                          AND column_name = 'FullSyncIntervalHours'
                    ) THEN
                        ALTER TABLE mail_archiver.""MailAccounts""
                        ADD COLUMN ""FullSyncIntervalHours"" integer NULL;
                        COMMENT ON COLUMN mail_archiver.""MailAccounts"".""FullSyncIntervalHours""
                            IS 'Per-account full sync interval in hours; NULL disables automatic full sync (only manual resync available)';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'MailAccounts'
                          AND column_name = 'LastFullSync'
                    ) THEN
                        ALTER TABLE mail_archiver.""MailAccounts""
                        ADD COLUMN ""LastFullSync"" timestamp without time zone NULL;
                        COMMENT ON COLUMN mail_archiver.""MailAccounts"".""LastFullSync""
                            IS 'UTC timestamp of the last automatic full sync; used together with FullSyncIntervalHours to schedule the next full sync';
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DROP COLUMN IF EXISTS also removes the column comments automatically.
            migrationBuilder.Sql(@"
                ALTER TABLE mail_archiver.""MailAccounts""
                DROP COLUMN IF EXISTS ""SyncIntervalMinutes"",
                DROP COLUMN IF EXISTS ""FullSyncIntervalHours"",
                DROP COLUMN IF EXISTS ""LastFullSync"";
            ");
        }
    }
}
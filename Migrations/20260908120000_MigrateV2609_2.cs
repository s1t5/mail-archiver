using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2609_2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // UID watermark for resumable folder syncs
            // ============================================================
            // A sync checkpoint used to remember only the Date header of the
            // last archived message, and the resume path fed that value into
            // an INTERNALDATE search while messages are processed in UID
            // order - two mismatches that could silently skip messages.
            // LastUid plus UidValidity replace that with a real watermark:
            // the folder search stays exactly what it would have been and
            // the UIDs at or below LastUid are dropped from its result.
            //
            // Both columns are nullable and stay NULL on existing rows, which
            // the resume path reads as "no usable watermark" and falls back to
            // reading the folder in full. No data migration needed, and no
            // checkpoint written before this migration is ever trusted.
            // Idempotent: only adds when missing.

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'SyncCheckpoints'
                          AND column_name = 'LastUid'
                    ) THEN
                        ALTER TABLE mail_archiver.""SyncCheckpoints""
                            ADD COLUMN ""LastUid"" bigint;

                        COMMENT ON COLUMN mail_archiver.""SyncCheckpoints"".""LastUid""
                            IS 'IMAP UID of the last message archived in this folder; NULL means no usable resume watermark';
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'SyncCheckpoints'
                          AND column_name = 'UidValidity'
                    ) THEN
                        ALTER TABLE mail_archiver.""SyncCheckpoints""
                            ADD COLUMN ""UidValidity"" bigint;

                        COMMENT ON COLUMN mail_archiver.""SyncCheckpoints"".""UidValidity""
                            IS 'Folder UIDVALIDITY when LastUid was recorded; a mismatch invalidates the checkpoint';
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'SyncCheckpoints'
                          AND column_name = 'LastUid'
                    ) THEN
                        ALTER TABLE mail_archiver.""SyncCheckpoints"" DROP COLUMN ""LastUid"";
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'SyncCheckpoints'
                          AND column_name = 'UidValidity'
                    ) THEN
                        ALTER TABLE mail_archiver.""SyncCheckpoints"" DROP COLUMN ""UidValidity"";
                    END IF;
                END $$;
            ");
        }
    }
}

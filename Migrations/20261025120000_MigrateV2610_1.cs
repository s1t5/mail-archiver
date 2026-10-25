using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2610_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // Original message storage (RawMessages:Enabled, opt-in)
            // ============================================================
            // One row per archived email holding the message as received,
            // gzip-compressed, served by GET /api/v1/emails/{id}/raw. Keyed by
            // the email id and removed with it (ON DELETE CASCADE), so every
            // deletion path - retention, account deletion, manual delete -
            // takes the original along without further code.
            //
            // The content is already compressed, so the column is stored
            // EXTERNAL: out of line, without another round of TOAST
            // compression. Hand-written idempotent SQL (house style).

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'mail_archiver' AND table_name = 'RawMessages'
                    ) THEN
                        CREATE TABLE mail_archiver.""RawMessages"" (
                            ""ArchivedEmailId"" integer NOT NULL,
                            ""Content"" bytea NOT NULL,
                            ""Sha256"" varchar(64) NOT NULL,
                            ""Size"" bigint NOT NULL,
                            ""Source"" varchar(16) NOT NULL,
                            ""CreatedAt"" timestamp without time zone NOT NULL,
                            CONSTRAINT ""PK_RawMessages"" PRIMARY KEY (""ArchivedEmailId""),
                            CONSTRAINT ""FK_RawMessages_ArchivedEmails_ArchivedEmailId"" FOREIGN KEY (""ArchivedEmailId"")
                                REFERENCES mail_archiver.""ArchivedEmails"" (""Id"") ON DELETE CASCADE
                        );

                        ALTER TABLE mail_archiver.""RawMessages""
                            ALTER COLUMN ""Content"" SET STORAGE EXTERNAL;

                        COMMENT ON TABLE mail_archiver.""RawMessages""
                            IS 'Original RFC 5322 message per archived email (gzip), only written when RawMessages:Enabled is set';
                        COMMENT ON COLUMN mail_archiver.""RawMessages"".""Sha256""
                            IS 'SHA-256 of the uncompressed message, lowercase hex';
                        COMMENT ON COLUMN mail_archiver.""RawMessages"".""Size""
                            IS 'Size of the uncompressed message in bytes';
                        COMMENT ON COLUMN mail_archiver.""RawMessages"".""Source""
                            IS 'imap, graph, eml-file (byte-exact) or serialized (re-written by MimeKit)';
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TABLE IF EXISTS mail_archiver.""RawMessages"";
            ");
        }
    }
}

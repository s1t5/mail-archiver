using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2609_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // Audit data export history table
            // ============================================================
            // Persists one row per audit data export run so the export
            // history survives restarts and can be displayed revision-safe
            // on the dedicated audit export page.
            // Idempotent: only creates when missing.

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'AuditExportJobs'
                    ) THEN
                        CREATE TABLE mail_archiver.""AuditExportJobs"" (
                            ""Id"" uuid NOT NULL,
                            ""Username"" text NOT NULL,
                            ""Created"" timestamp without time zone NOT NULL,
                            ""Started"" timestamp without time zone,
                            ""Completed"" timestamp without time zone,
                            ""Status"" character varying(20) NOT NULL DEFAULT 'Queued',
                            ""FromDate"" timestamp without time zone NOT NULL,
                            ""ToDate"" timestamp without time zone NOT NULL,
                            ""MailAccountId"" integer,
                            ""MailAccountName"" text,
                            ""IncludeAttachments"" boolean NOT NULL DEFAULT false,
                            ""DataSupplierName"" text,
                            ""DataSupplierLocation"" text,
                            ""DataSupplierComment"" text,
                            ""TotalEmails"" integer NOT NULL DEFAULT 0,
                            ""ProcessedEmails"" integer NOT NULL DEFAULT 0,
                            ""OutputFilePath"" text,
                            ""OutputFileSize"" bigint NOT NULL DEFAULT 0,
                            ""ErrorMessage"" text,
                            CONSTRAINT ""PK_AuditExportJobs"" PRIMARY KEY (""Id"")
                        );

                        COMMENT ON TABLE mail_archiver.""AuditExportJobs""
                            IS 'History of audit data export runs started from the audit export page';
                        COMMENT ON COLUMN mail_archiver.""AuditExportJobs"".""Id""
                            IS 'Job identifier (also used for status polling and download)';
                        COMMENT ON COLUMN mail_archiver.""AuditExportJobs"".""Username""
                            IS 'Admin user who started the export';
                        COMMENT ON COLUMN mail_archiver.""AuditExportJobs"".""Status""
                            IS 'Queued | Running | Completed | Failed | Cancelled | Downloaded';
                        COMMENT ON COLUMN mail_archiver.""AuditExportJobs"".""MailAccountId""
                            IS 'NULL = all mailboxes; MailAccountName is a snapshot of the account name at export time';
                        COMMENT ON COLUMN mail_archiver.""AuditExportJobs"".""IncludeAttachments""
                            IS 'Whether attachments.csv is part of the export';
                        COMMENT ON COLUMN mail_archiver.""AuditExportJobs"".""DataSupplierName"" IS 'Data supplier name as entered for this export';
                        COMMENT ON COLUMN mail_archiver.""AuditExportJobs"".""DataSupplierLocation"" IS 'Data supplier location as entered for this export';
                        COMMENT ON COLUMN mail_archiver.""AuditExportJobs"".""DataSupplierComment"" IS 'Free text comment as entered for this export';
                        COMMENT ON COLUMN mail_archiver.""AuditExportJobs"".""OutputFilePath""
                            IS 'Absolute path of the generated ZIP inside the container';
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE schemaname = 'mail_archiver'
                          AND indexname = 'IX_AuditExportJobs_Created'
                    ) THEN
                        CREATE INDEX ""IX_AuditExportJobs_Created""
                            ON mail_archiver.""AuditExportJobs"" (""Created"");
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM pg_indexes
                        WHERE schemaname = 'mail_archiver'
                          AND indexname = 'IX_AuditExportJobs_Status'
                    ) THEN
                        CREATE INDEX ""IX_AuditExportJobs_Status""
                            ON mail_archiver.""AuditExportJobs"" (""Status"");
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
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'AuditExportJobs'
                    ) THEN
                        DROP TABLE mail_archiver.""AuditExportJobs"";
                    END IF;
                END $$;
            ");
        }
    }
}
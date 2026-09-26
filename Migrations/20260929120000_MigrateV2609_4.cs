using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2609_4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // Dashboard statistics cache: direction splits and chart series
            // ============================================================
            // The dashboard counters carry their incoming and outgoing parts
            // and the account card the number of distinct domains, and the
            // charts were replaced by one series that knows its resolution,
            // period and direction. The pre-computed row has to hold what the
            // dashboard now shows, so the parts get their own columns and the
            // monthly histogram and the top senders give way to the default
            // chart selection, stored as one jsonb series.
            //
            // The row also records which Dashboard:ShowDirectionSplits and
            // Dashboard:SelectablePeriods values it was computed with. A row
            // from before this migration has neither, so it is passed over
            // until DashboardStatsRefreshService writes the next one, instead
            // of showing zeros where the parts belong. Idempotent.

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'DashboardStatsCache'
                    ) THEN
                        ALTER TABLE mail_archiver.""DashboardStatsCache""
                            ADD COLUMN IF NOT EXISTS ""IncomingEmails"" bigint NOT NULL DEFAULT 0,
                            ADD COLUMN IF NOT EXISTS ""OutgoingEmails"" bigint NOT NULL DEFAULT 0,
                            ADD COLUMN IF NOT EXISTS ""IncomingAttachments"" bigint NOT NULL DEFAULT 0,
                            ADD COLUMN IF NOT EXISTS ""OutgoingAttachments"" bigint NOT NULL DEFAULT 0,
                            ADD COLUMN IF NOT EXISTS ""AccountDomains"" integer NOT NULL DEFAULT 0,
                            ADD COLUMN IF NOT EXISTS ""DefaultSeriesJson"" jsonb,
                            ADD COLUMN IF NOT EXISTS ""ComputedWithDirectionSplits"" boolean,
                            ADD COLUMN IF NOT EXISTS ""ComputedWithSelectablePeriods"" boolean,
                            DROP COLUMN IF EXISTS ""TopSendersJson"",
                            DROP COLUMN IF EXISTS ""EmailsByMonthJson"";

                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""IncomingEmails""
                            IS 'Received mails at ComputedAtUtc (0 when computed without direction splits)';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""OutgoingEmails""
                            IS 'Sent mails at ComputedAtUtc (0 when computed without direction splits)';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""IncomingAttachments""
                            IS 'Attachments of received mails at ComputedAtUtc (0 when computed without direction splits)';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""OutgoingAttachments""
                            IS 'Attachments of sent mails at ComputedAtUtc (0 when computed without direction splits)';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""AccountDomains""
                            IS 'Distinct mail domains among the accounts (0 when computed without direction splits)';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""DefaultSeriesJson""
                            IS 'Default dashboard chart selection (months over one year, incoming senders) serialized as jsonb';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""ComputedWithDirectionSplits""
                            IS 'Dashboard:ShowDirectionSplits the row was computed with; a mismatching row is not used';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""ComputedWithSelectablePeriods""
                            IS 'Dashboard:SelectablePeriods the row was computed with; a mismatching row is not used';
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old list columns come back empty; the refresh service of the
            // older code fills them on its next run.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'DashboardStatsCache'
                    ) THEN
                        ALTER TABLE mail_archiver.""DashboardStatsCache""
                            ADD COLUMN IF NOT EXISTS ""TopSendersJson"" jsonb,
                            ADD COLUMN IF NOT EXISTS ""EmailsByMonthJson"" jsonb,
                            DROP COLUMN IF EXISTS ""IncomingEmails"",
                            DROP COLUMN IF EXISTS ""OutgoingEmails"",
                            DROP COLUMN IF EXISTS ""IncomingAttachments"",
                            DROP COLUMN IF EXISTS ""OutgoingAttachments"",
                            DROP COLUMN IF EXISTS ""AccountDomains"",
                            DROP COLUMN IF EXISTS ""DefaultSeriesJson"",
                            DROP COLUMN IF EXISTS ""ComputedWithDirectionSplits"",
                            DROP COLUMN IF EXISTS ""ComputedWithSelectablePeriods"";
                    END IF;
                END $$;
            ");
        }
    }
}

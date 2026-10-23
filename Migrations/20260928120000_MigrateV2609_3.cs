using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2609_3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // Pre-computed dashboard statistics cache
            // ============================================================
            // On installations with several million archived mails the
            // dashboard's aggregates (two full-table COUNTs, a GROUP BY
            // over all senders, the 12-month histogram, per-account
            // counts) take around ten seconds. Running them in the
            // request path made the first dashboard load after every
            // cache TTL expiry visibly slow. The DashboardStatsRefreshService
            // now computes these numbers in the background and stores
            // them here; the dashboard reads the prepared row instead.
            //
            // The list-shaped metrics (top senders, monthly histogram,
            // account panel) are stored as jsonb so one row per cache
            // key is enough. Idempotent: only creates when missing.

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.tables
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'DashboardStatsCache'
                    ) THEN
                        CREATE TABLE mail_archiver.""DashboardStatsCache"" (
                            ""Key"" text NOT NULL,
                            ""TotalEmails"" bigint NOT NULL DEFAULT 0,
                            ""TotalAttachments"" bigint NOT NULL DEFAULT 0,
                            ""TotalAccounts"" integer NOT NULL DEFAULT 0,
                            ""TotalDatabaseSizeBytes"" bigint NOT NULL DEFAULT 0,
                            ""TopSendersJson"" jsonb,
                            ""EmailsByMonthJson"" jsonb,
                            ""EmailsPerAccountJson"" jsonb,
                            ""ComputedAtUtc"" timestamp with time zone NOT NULL DEFAULT now(),
                            CONSTRAINT ""PK_DashboardStatsCache"" PRIMARY KEY (""Key"")
                        );

                        COMMENT ON TABLE mail_archiver.""DashboardStatsCache""
                            IS 'Pre-computed dashboard statistics, refreshed periodically by DashboardStatsRefreshService';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""Key""
                            IS 'Cache scope, e.g. admin';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""TotalEmails""
                            IS 'COUNT(*) over ArchivedEmails at ComputedAtUtc';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""TotalAttachments""
                            IS 'COUNT(*) over EmailAttachments at ComputedAtUtc';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""TotalAccounts""
                            IS 'COUNT(*) over MailAccounts at ComputedAtUtc';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""TotalDatabaseSizeBytes""
                            IS 'pg_database_size() at ComputedAtUtc';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""TopSendersJson""
                            IS 'Top 10 senders (incoming only) serialized as jsonb';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""EmailsByMonthJson""
                            IS 'Email counts for the last 12 months serialized as jsonb';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""EmailsPerAccountJson""
                            IS 'Dashboard account panel rows (max 25) serialized as jsonb';
                        COMMENT ON COLUMN mail_archiver.""DashboardStatsCache"".""ComputedAtUtc""
                            IS 'When the background service computed this row';
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
                          AND table_name = 'DashboardStatsCache'
                    ) THEN
                        DROP TABLE mail_archiver.""DashboardStatsCache"";
                    END IF;
                END $$;
            ");
        }
    }
}
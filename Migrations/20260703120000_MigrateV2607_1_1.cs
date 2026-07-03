using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2607_1_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // MSA (personal Microsoft account) OAuth2 token storage
            // ============================================================
            // Adds columns used by the device code flow (RFC 8628) for personal
            // Microsoft accounts (Outlook.com / M365 Family), which cannot use
            // the existing client-credentials M365 flow. The refresh token is
            // long-lived and persisted so syncs can run unattended; the access
            // token is cached and refreshed before each sync.

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'MailAccounts'
                          AND column_name = 'OAuthRefreshToken'
                    ) THEN
                        ALTER TABLE mail_archiver.""MailAccounts""
                        ADD COLUMN ""OAuthRefreshToken"" text NULL;
                        COMMENT ON COLUMN mail_archiver.""MailAccounts"".""OAuthRefreshToken""
                            IS 'MSA OAuth2 refresh token (personal Microsoft accounts, device code flow); long-lived, used to obtain new access tokens';
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
                          AND column_name = 'OAuthAccessToken'
                    ) THEN
                        ALTER TABLE mail_archiver.""MailAccounts""
                        ADD COLUMN ""OAuthAccessToken"" text NULL;
                        COMMENT ON COLUMN mail_archiver.""MailAccounts"".""OAuthAccessToken""
                            IS 'MSA OAuth2 access token (cached, short-lived); refreshed from the refresh token before each sync';
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
                          AND column_name = 'OAuthTokenExpiry'
                    ) THEN
                        ALTER TABLE mail_archiver.""MailAccounts""
                        ADD COLUMN ""OAuthTokenExpiry"" timestamp without time zone NULL;
                        COMMENT ON COLUMN mail_archiver.""MailAccounts"".""OAuthTokenExpiry""
                            IS 'UTC expiry of the cached MSA access token';
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
                DROP COLUMN IF EXISTS ""OAuthRefreshToken"",
                DROP COLUMN IF EXISTS ""OAuthAccessToken"",
                DROP COLUMN IF EXISTS ""OAuthTokenExpiry"";
            ");
        }
    }
}

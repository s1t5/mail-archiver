using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class AddMsaOAuthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE mail_archiver.""MailAccounts""
                DROP COLUMN IF EXISTS ""OAuthRefreshToken"",
                DROP COLUMN IF EXISTS ""OAuthAccessToken"",
                DROP COLUMN IF EXISTS ""OAuthTokenExpiry"";
            ");
        }
    }
}

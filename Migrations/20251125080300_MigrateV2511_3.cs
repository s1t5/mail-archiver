using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2511_3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Make PasswordHash nullable for OAuth users
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    ALTER TABLE mail_archiver.""Users"" 
                    ALTER COLUMN ""PasswordHash"" DROP NOT NULL;
                END $$;
            ");

            // Add OAuthRemoteUserId column for linking to external OAuth provider
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'Users' 
                        AND column_name = 'OAuthRemoteUserId'
                    ) THEN
                        ALTER TABLE mail_archiver.""Users"" ADD COLUMN ""OAuthRemoteUserId"" text;
                    END IF;
                END $$;
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""Users"".""OAuthRemoteUserId"" IS 'Remote user identifier from OAuth/OIDC provider (sub claim)';
            ");

            // Add RequiresApproval column for admin approval workflow
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'Users' 
                        AND column_name = 'RequiresApproval'
                    ) THEN
                        ALTER TABLE mail_archiver.""Users"" ADD COLUMN ""RequiresApproval"" boolean NOT NULL DEFAULT false;
                    END IF;
                END $$;
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""Users"".""RequiresApproval"" IS 'Indicates if OIDC-created user requires admin approval before being activated';
            ");

            // Create unique index on OAuthRemoteUserId to prevent duplicate OAuth accounts
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM pg_indexes 
                        WHERE schemaname = 'mail_archiver' 
                        AND tablename = 'Users' 
                        AND indexname = 'IX_Users_OAuthRemoteUserId'
                    ) THEN
                        CREATE UNIQUE INDEX ""IX_Users_OAuthRemoteUserId"" 
                        ON mail_archiver.""Users"" (""OAuthRemoteUserId"") 
                        WHERE ""OAuthRemoteUserId"" IS NOT NULL;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop unique index
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS mail_archiver.""IX_Users_OAuthRemoteUserId"";
            ");

            // Remove columns
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'Users' 
                        AND column_name = 'OAuthRemoteUserId'
                    ) THEN
                        ALTER TABLE mail_archiver.""Users"" DROP COLUMN ""OAuthRemoteUserId"";
                    END IF;

                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'Users' 
                        AND column_name = 'RequiresApproval'
                    ) THEN
                        ALTER TABLE mail_archiver.""Users"" DROP COLUMN ""RequiresApproval"";
                    END IF;
                END $$;
            ");

            // Make PasswordHash required again
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    ALTER TABLE mail_archiver.""Users"" 
                    ALTER COLUMN ""PasswordHash"" SET NOT NULL;
                END $$;
            ");
        }
    }
}

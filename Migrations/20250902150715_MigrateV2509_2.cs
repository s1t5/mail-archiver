using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2509_2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add 2FA columns to Users table if they don't exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_schema = 'mail_archiver' 
                                   AND table_name = 'Users' 
                                   AND column_name = 'IsTwoFactorEnabled') THEN
                        ALTER TABLE mail_archiver.""Users"" 
                        ADD COLUMN ""IsTwoFactorEnabled"" boolean NOT NULL DEFAULT FALSE;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_schema = 'mail_archiver' 
                                   AND table_name = 'Users' 
                                   AND column_name = 'TwoFactorSecret') THEN
                        ALTER TABLE mail_archiver.""Users"" 
                        ADD COLUMN ""TwoFactorSecret"" text;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_schema = 'mail_archiver' 
                                   AND table_name = 'Users' 
                                   AND column_name = 'TwoFactorBackupCodes') THEN
                        ALTER TABLE mail_archiver.""Users"" 
                        ADD COLUMN ""TwoFactorBackupCodes"" text;
                    END IF;
                END $$;
            ");

            // Add Microsoft 365 OAuth2 columns to MailAccounts table if they don't exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_schema = 'mail_archiver' 
                                   AND table_name = 'MailAccounts' 
                                   AND column_name = 'ClientId') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ADD COLUMN ""ClientId"" text;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_schema = 'mail_archiver' 
                                   AND table_name = 'MailAccounts' 
                                   AND column_name = 'ClientSecret') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ADD COLUMN ""ClientSecret"" text;
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_schema = 'mail_archiver' 
                                   AND table_name = 'MailAccounts' 
                                   AND column_name = 'TenantId') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ADD COLUMN ""TenantId"" text;
                    END IF;
                END $$;
            ");

            // Add Provider column to MailAccounts table if it doesn't exist and migrate existing data
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_schema = 'mail_archiver' 
                                   AND table_name = 'MailAccounts' 
                                   AND column_name = 'Provider') THEN
                        -- Add the Provider column
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ADD COLUMN ""Provider"" text;
                        
                        -- Migrate existing data
                        -- Since IsMicrosoft365 column doesn't exist in any existing database,
                        -- we'll set all existing accounts to 'IMAP' by default
                        -- If IsImportOnly is true, set Provider to 'IMPORT'
                        -- Otherwise, set Provider to 'IMAP' (default)
                        UPDATE mail_archiver.""MailAccounts"" 
                        SET ""Provider"" = CASE 
                            WHEN ""IsImportOnly"" = TRUE THEN 'IMPORT'
                            ELSE 'IMAP'
                        END;
                        
                        -- Set default for new records
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ALTER COLUMN ""Provider"" SET DEFAULT 'IMAP';
                    END IF;
                END $$;
            ");

            // Drop UserSessions table if it exists
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.tables 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'UserSessions') THEN
                        DROP TABLE mail_archiver.""UserSessions"";
                    END IF;
                END $$;
            ");

            // Recreate UserSessions table if it doesn't exist (for rollback)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables 
                                   WHERE table_schema = 'mail_archiver' 
                                   AND table_name = 'UserSessions') THEN
                        CREATE TABLE mail_archiver.""UserSessions"" (
                            ""Id"" serial PRIMARY KEY,
                            ""Token"" character varying(255) NOT NULL,
                            ""Username"" character varying(50) NOT NULL,
                            ""CreatedAt"" timestamp without time zone NOT NULL,
                            ""ExpiresAt"" timestamp without time zone
                        );
                        
                        -- Create indexes
                        CREATE UNIQUE INDEX ""ix_usersessions_token""
                        ON mail_archiver.""UserSessions"" (""Token"");
                        
                        CREATE INDEX ""ix_usersessions_username""
                        ON mail_archiver.""UserSessions"" (""Username"");
                        
                        CREATE INDEX ""ix_usersessions_expiresat""
                        ON mail_archiver.""UserSessions"" (""ExpiresAt"");
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove 2FA columns from Users table if they exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'Users' 
                               AND column_name = 'IsTwoFactorEnabled') THEN
                        ALTER TABLE mail_archiver.""Users"" 
                        DROP COLUMN ""IsTwoFactorEnabled"";
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'Users' 
                               AND column_name = 'TwoFactorSecret') THEN
                        ALTER TABLE mail_archiver.""Users"" 
                        DROP COLUMN ""TwoFactorSecret"";
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'Users' 
                               AND column_name = 'TwoFactorBackupCodes') THEN
                        ALTER TABLE mail_archiver.""Users"" 
                        DROP COLUMN ""TwoFactorBackupCodes"";
                    END IF;
                END $$;
            ");

            // Remove Microsoft 365 OAuth2 columns from MailAccounts table if they exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'MailAccounts' 
                               AND column_name = 'ClientId') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        DROP COLUMN ""ClientId"";
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'MailAccounts' 
                               AND column_name = 'ClientSecret') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        DROP COLUMN ""ClientSecret"";
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'MailAccounts' 
                               AND column_name = 'TenantId') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        DROP COLUMN ""TenantId"";
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'MailAccounts' 
                               AND column_name = 'Provider') THEN
                        -- Add back IsImportOnly column if it doesn't exist
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                       WHERE table_schema = 'mail_archiver' 
                                       AND table_name = 'MailAccounts' 
                                       AND column_name = 'IsImportOnly') THEN
                            ALTER TABLE mail_archiver.""MailAccounts"" 
                            ADD COLUMN ""IsImportOnly"" boolean;
                            
                            -- Migrate data back from Provider to IsImportOnly
                            UPDATE mail_archiver.""MailAccounts"" 
                            SET ""IsImportOnly"" = CASE 
                                WHEN ""Provider"" = 'IMPORT' THEN TRUE
                                ELSE FALSE
                            END;
                            
                            -- Set default for new records
                            ALTER TABLE mail_archiver.""MailAccounts"" 
                            ALTER COLUMN ""IsImportOnly"" SET DEFAULT FALSE;
                        END IF;
                        
                        -- Drop the Provider column
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        DROP COLUMN ""Provider"";
                    END IF;
                END $$;
            ");
        }
    }
}

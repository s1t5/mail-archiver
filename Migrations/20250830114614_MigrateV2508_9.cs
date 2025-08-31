using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2508_9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add IsSelfManager column to Users table if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_schema = 'mail_archiver' 
                                   AND table_name = 'Users' 
                                   AND column_name = 'IsSelfManager') THEN
                        ALTER TABLE mail_archiver.""Users"" 
                        ADD COLUMN ""IsSelfManager"" boolean NOT NULL DEFAULT FALSE;
                    END IF;
                END $$;
            ");

            // Add IsImportOnly column to MailAccounts table if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_schema = 'mail_archiver' 
                                   AND table_name = 'MailAccounts' 
                                   AND column_name = 'IsImportOnly') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ADD COLUMN ""IsImportOnly"" boolean NOT NULL DEFAULT FALSE;
                    END IF;
                END $$;
            ");

            // Make IMAP fields nullable for import-only accounts (only if column exists)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Check if ImapServer column exists and alter it
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'MailAccounts' 
                               AND column_name = 'ImapServer') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ALTER COLUMN ""ImapServer"" TYPE text,
                        ALTER COLUMN ""ImapServer"" DROP NOT NULL;
                    END IF;
                    
                    -- Check if ImapPort column exists and alter it
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'MailAccounts' 
                               AND column_name = 'ImapPort') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ALTER COLUMN ""ImapPort"" TYPE integer USING ""ImapPort""::integer,
                        ALTER COLUMN ""ImapPort"" DROP NOT NULL;
                    END IF;
                    
                    -- Check if Username column exists and alter it
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'MailAccounts' 
                               AND column_name = 'Username') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ALTER COLUMN ""Username"" TYPE text,
                        ALTER COLUMN ""Username"" DROP NOT NULL;
                    END IF;
                    
                    -- Check if Password column exists and alter it
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'MailAccounts' 
                               AND column_name = 'Password') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ALTER COLUMN ""Password"" TYPE text,
                        ALTER COLUMN ""Password"" DROP NOT NULL;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert IMAP fields back to not nullable (only if column exists)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Check if ImapServer column exists and alter it back to not nullable
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'MailAccounts' 
                               AND column_name = 'ImapServer') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ALTER COLUMN ""ImapServer"" TYPE text,
                        ALTER COLUMN ""ImapServer"" SET NOT NULL;
                    END IF;
                    
                    -- Check if ImapPort column exists and alter it back to not nullable
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'MailAccounts' 
                               AND column_name = 'ImapPort') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ALTER COLUMN ""ImapPort"" TYPE integer USING ""ImapPort""::integer,
                        ALTER COLUMN ""ImapPort"" SET NOT NULL;
                    END IF;
                    
                    -- Check if Username column exists and alter it back to not nullable
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'MailAccounts' 
                               AND column_name = 'Username') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ALTER COLUMN ""Username"" TYPE text,
                        ALTER COLUMN ""Username"" SET NOT NULL;
                    END IF;
                    
                    -- Check if Password column exists and alter it back to not nullable
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'MailAccounts' 
                               AND column_name = 'Password') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ALTER COLUMN ""Password"" TYPE text,
                        ALTER COLUMN ""Password"" SET NOT NULL;
                    END IF;
                END $$;
            ");

            // Remove IsSelfManager column from Users table if it exists
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'Users' 
                               AND column_name = 'IsSelfManager') THEN
                        ALTER TABLE mail_archiver.""Users"" 
                        DROP COLUMN ""IsSelfManager"";
                    END IF;
                END $$;
            ");

            // Remove IsImportOnly column from MailAccounts table if it exists
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'MailAccounts' 
                               AND column_name = 'IsImportOnly') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        DROP COLUMN ""IsImportOnly"";
                    END IF;
                END $$;
            ");
        }
    }
}

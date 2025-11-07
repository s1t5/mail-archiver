using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2511_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add ContentHash column
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'ContentHash'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" ADD COLUMN ""ContentHash"" varchar(64);
                    END IF;
                END $$;
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""ContentHash"" IS 'SHA-256 hash for integrity verification';
            ");

            // Add HashCreatedAt column
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'HashCreatedAt'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" ADD COLUMN ""HashCreatedAt"" timestamp with time zone;
                    END IF;
                END $$;
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""HashCreatedAt"" IS 'Timestamp when content hash was created';
            ");

            // Add IsLocked column
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'IsLocked'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" ADD COLUMN ""IsLocked"" boolean NOT NULL DEFAULT true;
                    END IF;
                END $$;
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""IsLocked"" IS 'Indicates if email is locked and cannot be modified (compliance)';
            ");

            // Add BodyUntruncatedText column for full untruncated text body (not in search index)
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'BodyUntruncatedText'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" ADD COLUMN ""BodyUntruncatedText"" text;
                    END IF;
                END $$;
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""BodyUntruncatedText"" IS 'Full untruncated text body (not indexed for search due to size limits)';
            ");

            // Add BodyUntruncatedHtml column for full untruncated HTML body (not in search index)
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'BodyUntruncatedHtml'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" ADD COLUMN ""BodyUntruncatedHtml"" text;
                    END IF;
                END $$;
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""BodyUntruncatedHtml"" IS 'Full untruncated HTML body (not indexed for search due to size limits)';
            ");

            // Create index for ContentHash
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM pg_indexes 
                        WHERE schemaname = 'mail_archiver' 
                        AND tablename = 'ArchivedEmails' 
                        AND indexname = 'IX_ArchivedEmails_ContentHash'
                    ) THEN
                        CREATE INDEX ""IX_ArchivedEmails_ContentHash"" ON mail_archiver.""ArchivedEmails"" (""ContentHash"");
                    END IF;
                END $$;
            ");

            // Create trigger function to prevent modifications to locked emails
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION mail_archiver.prevent_locked_email_changes()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF OLD.""IsLocked"" = true AND (
                        NEW.""Subject"" IS DISTINCT FROM OLD.""Subject"" OR
                        NEW.""Body"" IS DISTINCT FROM OLD.""Body"" OR
                        NEW.""From"" IS DISTINCT FROM OLD.""From"" OR
                        NEW.""To"" IS DISTINCT FROM OLD.""To"" OR
                        NEW.""ContentHash"" IS DISTINCT FROM OLD.""ContentHash""
                    ) THEN
                        RAISE EXCEPTION 'Email is locked and cannot be modified (compliance requirement)';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Create trigger for UPDATE
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_trigger 
                        WHERE tgname = 'prevent_locked_email_updates'
                    ) THEN
                        CREATE TRIGGER prevent_locked_email_updates
                            BEFORE UPDATE ON mail_archiver.""ArchivedEmails""
                            FOR EACH ROW
                            EXECUTE FUNCTION mail_archiver.prevent_locked_email_changes();
                    END IF;
                END $$;
            ");

            // Create trigger function to prevent deletion of locked emails
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION mail_archiver.prevent_locked_email_deletion()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF OLD.""IsLocked"" = true THEN
                        RAISE EXCEPTION 'Email is locked and cannot be deleted (compliance requirement - retention period active)';
                    END IF;
                    RETURN OLD;
                END;
                $$ LANGUAGE plpgsql;
            ");

            // Create trigger for DELETE
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_trigger 
                        WHERE tgname = 'prevent_locked_email_deletion'
                    ) THEN
                        CREATE TRIGGER prevent_locked_email_deletion
                            BEFORE DELETE ON mail_archiver.""ArchivedEmails""
                            FOR EACH ROW
                            EXECUTE FUNCTION mail_archiver.prevent_locked_email_deletion();
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop DELETE trigger
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS prevent_locked_email_deletion ON mail_archiver.""ArchivedEmails"";
            ");

            // Drop DELETE trigger function
            migrationBuilder.Sql(@"
                DROP FUNCTION IF EXISTS mail_archiver.prevent_locked_email_deletion();
            ");

            // Drop UPDATE trigger
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS prevent_locked_email_updates ON mail_archiver.""ArchivedEmails"";
            ");

            // Drop UPDATE trigger function
            migrationBuilder.Sql(@"
                DROP FUNCTION IF EXISTS mail_archiver.prevent_locked_email_changes();
            ");

            // Drop index
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS mail_archiver.""IX_ArchivedEmails_ContentHash"";
            ");

            // Remove columns
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'ContentHash'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" DROP COLUMN ""ContentHash"";
                    END IF;

                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'HashCreatedAt'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" DROP COLUMN ""HashCreatedAt"";
                    END IF;

                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'IsLocked'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" DROP COLUMN ""IsLocked"";
                    END IF;

                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'BodyUntruncatedText'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" DROP COLUMN ""BodyUntruncatedText"";
                    END IF;

                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'BodyUntruncatedHtml'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" DROP COLUMN ""BodyUntruncatedHtml"";
                    END IF;
                END $$;
            ");
        }
    }
}

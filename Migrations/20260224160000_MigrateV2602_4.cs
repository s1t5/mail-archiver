using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2602_4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add OriginalBodyText column to ArchivedEmails table for storing original body with null bytes
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'OriginalBodyText'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" ADD COLUMN ""OriginalBodyText"" bytea;
                    END IF;
                END $$;
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""OriginalBodyText"" IS 'Original text body content including null bytes (stored as bytea). Only populated when the original body contained null bytes that needed to be cleaned for PostgreSQL TEXT storage.';
            ");

            // Add OriginalBodyHtml column to ArchivedEmails table for storing original body with null bytes
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'OriginalBodyHtml'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" ADD COLUMN ""OriginalBodyHtml"" bytea;
                    END IF;
                END $$;
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""OriginalBodyHtml"" IS 'Original HTML body content including null bytes (stored as bytea). Only populated when the original body contained null bytes that needed to be cleaned for PostgreSQL TEXT storage.';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove OriginalBodyHtml column
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'OriginalBodyHtml'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" DROP COLUMN ""OriginalBodyHtml"";
                    END IF;
                END $$;
            ");

            // Remove OriginalBodyText column
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'OriginalBodyText'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" DROP COLUMN ""OriginalBodyText"";
                    END IF;
                END $$;
            ");
        }
    }
}
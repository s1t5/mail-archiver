using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2602_2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add RawHeaders column to ArchivedEmails table for storing complete original email headers
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'RawHeaders'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" ADD COLUMN ""RawHeaders"" text;
                    END IF;
                END $$;
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""RawHeaders"" IS 'Complete raw email headers as stored in the original email, including Received, Return-Path, X-Headers, etc. Stored for forensic and compliance purposes.';
            ");

            // Add Bcc column to ArchivedEmails table if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'Bcc'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" ADD COLUMN ""Bcc"" text NOT NULL DEFAULT '';
                    END IF;
                END $$;
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""Bcc"" IS 'Blind carbon copy recipients of the email';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove RawHeaders column
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'RawHeaders'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" DROP COLUMN ""RawHeaders"";
                    END IF;
                END $$;
            ");

            // Remove Bcc column
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'ArchivedEmails' 
                        AND column_name = 'Bcc'
                    ) THEN
                        ALTER TABLE mail_archiver.""ArchivedEmails"" DROP COLUMN ""Bcc"";
                    END IF;
                END $$;
            ");
        }
    }
}
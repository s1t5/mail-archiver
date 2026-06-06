using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2606_1_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add LastSeenChangelogVersion column to Users table for version update splash screen tracking
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'Users'
                        AND column_name = 'LastSeenChangelogVersion'
                    ) THEN
                        ALTER TABLE mail_archiver.""Users"" 
                        ADD COLUMN ""LastSeenChangelogVersion"" varchar(20) NULL;
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
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'Users'
                        AND column_name = 'LastSeenChangelogVersion'
                    ) THEN
                        ALTER TABLE mail_archiver.""Users"" 
                        DROP COLUMN ""LastSeenChangelogVersion"";
                    END IF;
                END $$;
            ");
        }
    }
}
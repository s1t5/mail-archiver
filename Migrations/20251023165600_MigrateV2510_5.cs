using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2510_5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add LocalRetentionDays column to MailAccounts table
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'MailAccounts' 
                        AND column_name = 'LocalRetentionDays'
                    ) THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" ADD COLUMN ""LocalRetentionDays"" integer;
                    END IF;
                END $$;
            ");
            
            // Add comment for documentation
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""MailAccounts"".""LocalRetentionDays"" IS 'Number of days after which emails are deleted from local archive (optional)';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove LocalRetentionDays column
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'MailAccounts' 
                        AND column_name = 'LocalRetentionDays'
                    ) THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" DROP COLUMN ""LocalRetentionDays"";
                    END IF;
                END $$;
            ");
        }
    }
}

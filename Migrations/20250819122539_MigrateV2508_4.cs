using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2508_4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add DeleteAfterDays column to MailAccounts table if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                                   WHERE table_schema = 'mail_archiver' 
                                   AND table_name = 'MailAccounts' 
                                   AND column_name = 'DeleteAfterDays') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        ADD COLUMN ""DeleteAfterDays"" integer;
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove DeleteAfterDays column if it exists
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'MailAccounts' 
                               AND column_name = 'DeleteAfterDays') THEN
                        ALTER TABLE mail_archiver.""MailAccounts"" 
                        DROP COLUMN ""DeleteAfterDays"";
                    END IF;
                END $$;
            ");
        }
    }
}

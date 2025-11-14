using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2511_2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update all existing emails to be unlocked (IsLocked = false) where ContentHash is null
            migrationBuilder.Sql(@"
                UPDATE mail_archiver.""ArchivedEmails"" 
                SET ""IsLocked"" = false 
                WHERE ""IsLocked"" = true AND ""ContentHash"" IS NULL;
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""IsLocked"" IS 'Indicates if email is locked and cannot be modified (compliance)';
            ");

            // Change the default value of IsLocked column to false
            migrationBuilder.Sql(@"
                ALTER TABLE mail_archiver.""ArchivedEmails"" 
                ALTER COLUMN ""IsLocked"" SET DEFAULT false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert the default value of IsLocked column to true
            migrationBuilder.Sql(@"
                ALTER TABLE mail_archiver.""ArchivedEmails"" 
                ALTER COLUMN ""IsLocked"" SET DEFAULT true;
            ");

            // Update all emails back to locked (IsLocked = true) where ContentHash is null
            migrationBuilder.Sql(@"
                UPDATE mail_archiver.""ArchivedEmails"" 
                SET ""IsLocked"" = true
                WHERE ""ContentHash"" IS NULL;
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""ArchivedEmails"".""IsLocked"" IS 'Indicates if email is locked and cannot be modified (compliance)';
            ");
        }
    }
}

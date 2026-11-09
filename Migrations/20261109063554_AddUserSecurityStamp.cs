using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <summary>
    /// Adds the per-user security stamp that auth cookies are validated against.
    /// Existing rows get a unique value, otherwise every session (and every future
    /// login) would carry an empty stamp and be rejected.
    /// </summary>
    public partial class AddUserSecurityStamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'Users'
                          AND column_name = 'SecurityStamp'
                    ) THEN
                        ALTER TABLE mail_archiver.""Users""
                            ADD COLUMN ""SecurityStamp"" character varying(32) NOT NULL DEFAULT '';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                UPDATE mail_archiver.""Users""
                SET ""SecurityStamp"" = md5(random()::text || clock_timestamp()::text || ""Id""::text)
                WHERE ""SecurityStamp"" = '';
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE mail_archiver.""Users"" ALTER COLUMN ""SecurityStamp"" DROP DEFAULT;
            ");

            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""Users"".""SecurityStamp"" IS
                    'Rotated on password change, password reset, activation status and role changes. Auth cookies carrying an older value are rejected.';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE mail_archiver.""Users"" DROP COLUMN IF EXISTS ""SecurityStamp"";
            ");
        }
    }
}

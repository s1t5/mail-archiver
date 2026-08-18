using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2608_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // Widen Users.Username from varchar(50) to varchar(320)
            // ============================================================
            // OIDC auto-provisioning builds a local username from claims
            // (email local-part / sub / oidc_<hash>). With long Entra ID
            // UPNs the generated value exceeded varchar(50) and the INSERT
            // failed with PostgreSQL error 22001. varchar(320) safely
            // covers any RFC-5321 max-length email plus uniqueness suffix.
            // Idempotent: only alters when the current type is narrower.

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'Users'
                          AND column_name = 'Username'
                          AND character_maximum_length = 50
                    ) THEN
                        ALTER TABLE mail_archiver.""Users""
                        ALTER COLUMN ""Username"" TYPE character varying(320);
                        COMMENT ON COLUMN mail_archiver.""Users"".""Username""
                            IS 'Unique login name (local users) or generated stable name for OIDC-provisioned users; max 320 chars';
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NOTE: rolling back truncates any username longer than 50 chars,
            // which would fail if such rows exist. Kept for schema symmetry;
            // do not run Down() on a populated database without checking.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                          AND table_name = 'Users'
                          AND column_name = 'Username'
                          AND character_maximum_length = 320
                    ) THEN
                        ALTER TABLE mail_archiver.""Users""
                        ALTER COLUMN ""Username"" TYPE character varying(50);
                        COMMENT ON COLUMN mail_archiver.""Users"".""Username""
                            IS 'Unique username; max 50 chars';
                    END IF;
                END $$;
            ");
        }
    }
}
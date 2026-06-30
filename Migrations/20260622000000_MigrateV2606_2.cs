using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2606_2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add StorageType column to AttachmentContents.
            // 0 = Database (default)
            // All existing rows have their content in the database, so DEFAULT 0 is correct.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                        AND table_name = 'AttachmentContents'
                        AND column_name = 'StorageType'
                    ) THEN
                        ALTER TABLE mail_archiver.""AttachmentContents""
                        ADD COLUMN ""StorageType"" smallint NOT NULL DEFAULT 0;
                    END IF;
                END $$;
            ");

            // Allow Content to be NULL so that non-database-backed rows can omit the bytea payload.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                        AND table_name = 'AttachmentContents'
                        AND column_name = 'Content'
                        AND is_nullable = 'NO'
                    ) THEN
                        ALTER TABLE mail_archiver.""AttachmentContents""
                        ALTER COLUMN ""Content"" DROP NOT NULL;
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
                        AND table_name = 'AttachmentContents'
                        AND column_name = 'StorageType'
                    ) THEN
                        ALTER TABLE mail_archiver.""AttachmentContents""
                        DROP COLUMN ""StorageType"";
                    END IF;
                END $$;
            ");

            //  Only re-add NOT NULL if all rows actually have content
            // (this may fail if file-backed rows exist with NULL Content).
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver'
                        AND table_name = 'AttachmentContents'
                        AND column_name = 'Content'
                        AND is_nullable = 'YES'
                    ) THEN
                        ALTER TABLE mail_archiver.""AttachmentContents""
                        ALTER COLUMN ""Content"" SET NOT NULL;
                    END IF;
                END $$;
            ");
        }
    }
}

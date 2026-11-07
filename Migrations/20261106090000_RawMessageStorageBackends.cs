using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <summary>
    /// Adds the storage backend columns to <c>RawMessages</c> so the message bytes can live
    /// outside the database. Existing rows are Postgres rows and are backfilled as such, so an
    /// upgrade changes nothing until a storage migration is run explicitly.
    /// </summary>
    /// <remarks>
    /// The <c>[DbContext]</c> and <c>[Migration]</c> attributes that register this class with
    /// EF Core live in the companion <c>.Designer.cs</c> file, as they do for every other
    /// migration in this folder. Without them EF never discovers the migration and silently
    /// skips it, so the columns would never be created.
    /// </remarks>
    public partial class RawMessageStorageBackends : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // RawMessages: pluggable storage backend
            // ============================================================
            // Content becomes nullable: a filesystem-backed row carries only metadata, the
            // bytes are in the file named by StoragePath. Both new columns are additive and
            // default to the previous behaviour, so no row changes meaning on upgrade.

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver' AND table_name = 'RawMessages'
                          AND column_name = 'StorageType'
                    ) THEN
                        ALTER TABLE mail_archiver.""RawMessages""
                            ADD COLUMN ""StorageType"" varchar(16) NOT NULL DEFAULT 'postgres',
                            ADD COLUMN ""StoragePath"" text NULL;
                    END IF;

                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns
                        WHERE table_schema = 'mail_archiver' AND table_name = 'RawMessages'
                          AND column_name = 'Content' AND is_nullable = 'NO'
                    ) THEN
                        ALTER TABLE mail_archiver.""RawMessages""
                            ALTER COLUMN ""Content"" DROP NOT NULL;
                    END IF;

                    COMMENT ON COLUMN mail_archiver.""RawMessages"".""StorageType""
                        IS 'postgres (bytes in Content) or filesystem (bytes in StoragePath)';
                    COMMENT ON COLUMN mail_archiver.""RawMessages"".""StoragePath""
                        IS 'Path relative to RawMessages:FileSystem:BasePath, set for filesystem-backed rows';

                    -- Existing rows predate the column and keep their bytes in Content.
                    UPDATE mail_archiver.""RawMessages""
                       SET ""StorageType"" = 'postgres'
                     WHERE ""StorageType"" IS NULL OR ""StorageType"" = '';

                    CREATE INDEX IF NOT EXISTS ""IX_RawMessages_StorageType""
                        ON mail_archiver.""RawMessages"" (""StorageType"");
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverting requires every row to be Postgres-backed again; a filesystem-backed
            // row has no bytes in the column and would violate the NOT NULL constraint.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM mail_archiver.""RawMessages""
                        WHERE ""StorageType"" = 'filesystem'
                    ) THEN
                        RAISE EXCEPTION
                            'Cannot revert: filesystem-backed raw messages exist. Run the storage migration to postgres first.';
                    END IF;

                    DROP INDEX IF EXISTS mail_archiver.""IX_RawMessages_StorageType"";

                    ALTER TABLE mail_archiver.""RawMessages""
                        ALTER COLUMN ""Content"" SET NOT NULL;

                    ALTER TABLE mail_archiver.""RawMessages""
                        DROP COLUMN IF EXISTS ""StoragePath"",
                        DROP COLUMN IF EXISTS ""StorageType"";
                END $$;
            ");
        }
    }
}

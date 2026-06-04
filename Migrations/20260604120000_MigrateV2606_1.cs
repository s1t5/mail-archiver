using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2606_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // Attachment deduplication
            // ============================================================
            // Introduces content-addressed storage for attachment payloads so that
            // identical attachment bytes are stored exactly once and shared between
            // many EmailAttachment rows. Existing inline payloads (EmailAttachments.Content)
            // are migrated batch-by-batch by the resumable AttachmentDeduplicationBackgroundService.

            // pgcrypto provides digest() used by the background migration to hash payloads in-DB.
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            // Create the content-addressed storage table
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.tables 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'AttachmentContents'
                    ) THEN
                        CREATE TABLE mail_archiver.""AttachmentContents"" (
                            ""Id"" serial PRIMARY KEY,
                            ""Hash"" varchar(64) NOT NULL,
                            ""Content"" bytea NOT NULL,
                            ""Size"" bigint NOT NULL,
                            ""CreatedAt"" timestamp without time zone NOT NULL DEFAULT now()
                        );

                    END IF;
                END $$;
            ");

            // Unique index on Hash (guarantees a single physical copy per unique payload)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM pg_indexes 
                        WHERE schemaname = 'mail_archiver' 
                        AND indexname = 'IX_AttachmentContents_Hash'
                    ) THEN
                        CREATE UNIQUE INDEX ""IX_AttachmentContents_Hash"" 
                        ON mail_archiver.""AttachmentContents"" (""Hash"");
                    END IF;
                END $$;
            ");

            // Add the FK column on EmailAttachments
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'EmailAttachments'
                        AND column_name = 'AttachmentContentId'
                    ) THEN
                        ALTER TABLE mail_archiver.""EmailAttachments"" 
                        ADD COLUMN ""AttachmentContentId"" integer NULL;
                    END IF;
                END $$;
            ");

            // Make the legacy inline Content column nullable so migrated rows can be NULLed
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'EmailAttachments'
                        AND column_name = 'Content'
                        AND is_nullable = 'NO'
                    ) THEN
                        ALTER TABLE mail_archiver.""EmailAttachments"" 
                        ALTER COLUMN ""Content"" DROP NOT NULL;
                    END IF;
                END $$;
            ");

            // Index on the FK column
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM pg_indexes 
                        WHERE schemaname = 'mail_archiver' 
                        AND indexname = 'IX_EmailAttachments_AttachmentContentId'
                    ) THEN
                        CREATE INDEX ""IX_EmailAttachments_AttachmentContentId"" 
                        ON mail_archiver.""EmailAttachments"" (""AttachmentContentId"");
                    END IF;
                END $$;
            ");

            // Foreign key constraint (ON DELETE RESTRICT: content rows can only be removed
            // by the orphan garbage collection once no attachment references them)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.table_constraints 
                        WHERE constraint_type = 'FOREIGN KEY' 
                        AND table_schema = 'mail_archiver'
                        AND constraint_name = 'FK_EmailAttachments_AttachmentContents_AttachmentContentId'
                    ) THEN
                        ALTER TABLE mail_archiver.""EmailAttachments"" 
                        ADD CONSTRAINT ""FK_EmailAttachments_AttachmentContents_AttachmentContentId"" 
                        FOREIGN KEY (""AttachmentContentId"") 
                        REFERENCES mail_archiver.""AttachmentContents""(""Id"") 
                        ON DELETE RESTRICT;
                    END IF;
                END $$;
            ");

            // State table that makes the background data migration resumable across restarts
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.tables 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'AttachmentDeduplicationState'
                    ) THEN
                        CREATE TABLE mail_archiver.""AttachmentDeduplicationState"" (
                            ""Id"" integer PRIMARY KEY,
                            ""LastProcessedId"" bigint NOT NULL DEFAULT 0,
                            ""ProcessedCount"" bigint NOT NULL DEFAULT 0,
                            ""IsCompleted"" boolean NOT NULL DEFAULT false,
                            ""StartedAt"" timestamp without time zone NOT NULL DEFAULT now(),
                            ""UpdatedAt"" timestamp without time zone NOT NULL DEFAULT now(),
                            ""CompletedAt"" timestamp without time zone NULL,
                            ""ReclaimedAt"" timestamp without time zone NULL
                        );

                        INSERT INTO mail_archiver.""AttachmentDeduplicationState""
                            (""Id"", ""LastProcessedId"", ""ProcessedCount"", ""IsCompleted"", ""StartedAt"", ""UpdatedAt"")
                        VALUES (1, 0, 0, false, now(), now());
                    END IF;
                END $$;
            ");

            // For existing installations whose state table was created by an earlier
            // version of this migration: add the space-reclaim marker if it is missing.
            // ""ReclaimedAt"" records when the one-time VACUUM FULL of EmailAttachments
            // (which physically frees the now-NULLed legacy inline payloads) has run.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.tables 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'AttachmentDeduplicationState'
                    ) THEN
                        ALTER TABLE mail_archiver.""AttachmentDeduplicationState""
                        ADD COLUMN IF NOT EXISTS ""ReclaimedAt"" timestamp without time zone NULL;
                    END IF;
                END $$;
            ");


            // Comments
            migrationBuilder.Sql(@"
                COMMENT ON TABLE mail_archiver.""AttachmentContents"" IS 'Content-addressed (SHA-256) storage for deduplicated attachment payloads';
            ");
            migrationBuilder.Sql(@"
                COMMENT ON TABLE mail_archiver.""AttachmentDeduplicationState"" IS 'Tracks progress of the resumable background migration that deduplicates existing attachment payloads';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NOTE: A full down-migration would have to re-inline the deduplicated payloads
            // back into EmailAttachments.Content before dropping AttachmentContents. This is
            // done best-effort so the schema can be reverted without data loss.

            // Re-inline shared payloads back into the legacy Content column
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.tables 
                        WHERE table_schema = 'mail_archiver' AND table_name = 'AttachmentContents'
                    ) THEN
                        UPDATE mail_archiver.""EmailAttachments"" e
                        SET ""Content"" = ac.""Content""
                        FROM mail_archiver.""AttachmentContents"" ac
                        WHERE e.""AttachmentContentId"" = ac.""Id""
                          AND e.""Content"" IS NULL;
                    END IF;
                END $$;
            ");

            // Drop FK constraint
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.table_constraints 
                        WHERE constraint_type = 'FOREIGN KEY' 
                        AND table_schema = 'mail_archiver'
                        AND constraint_name = 'FK_EmailAttachments_AttachmentContents_AttachmentContentId'
                    ) THEN
                        ALTER TABLE mail_archiver.""EmailAttachments"" 
                        DROP CONSTRAINT ""FK_EmailAttachments_AttachmentContents_AttachmentContentId"";
                    END IF;
                END $$;
            ");

            // Drop FK column index + column
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS mail_archiver.""IX_EmailAttachments_AttachmentContentId"";");
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM information_schema.columns 
                        WHERE table_schema = 'mail_archiver' AND table_name = 'EmailAttachments' AND column_name = 'AttachmentContentId'
                    ) THEN
                        ALTER TABLE mail_archiver.""EmailAttachments"" DROP COLUMN ""AttachmentContentId"";
                    END IF;
                END $$;
            ");

            // Drop dedup tables
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS mail_archiver.""AttachmentDeduplicationState"";");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS mail_archiver.""AttachmentContents"";");

            // Restore NOT NULL on legacy Content column (only if all rows now have a value)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM mail_archiver.""EmailAttachments"" WHERE ""Content"" IS NULL
                    ) THEN
                        ALTER TABLE mail_archiver.""EmailAttachments"" ALTER COLUMN ""Content"" SET NOT NULL;
                    END IF;
                END $$;
            ");
        }
    }
}

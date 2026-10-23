using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2604_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create BandwidthUsage table for tracking IMAP bandwidth per account
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.tables 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'BandwidthUsage'
                    ) THEN
                        CREATE TABLE mail_archiver.""BandwidthUsage"" (
                            ""Id"" serial PRIMARY KEY,
                            ""MailAccountId"" integer NOT NULL,
                            ""Date"" date NOT NULL,
                            ""BytesDownloaded"" bigint NOT NULL DEFAULT 0,
                            ""BytesUploaded"" bigint NOT NULL DEFAULT 0,
                            ""EmailsProcessed"" integer NOT NULL DEFAULT 0,
                            ""LimitReached"" boolean NOT NULL DEFAULT false,
                            ""LimitResetTime"" timestamp NULL,
                            ""CreatedAt"" timestamp NOT NULL,
                            ""UpdatedAt"" timestamp NOT NULL
                        );
                    END IF;
                END $$;
            ");
            
            // Create unique index on MailAccountId and Date
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM pg_indexes 
                        WHERE schemaname = 'mail_archiver' 
                        AND indexname = 'IX_BandwidthUsage_Account_Date'
                    ) THEN
                        CREATE UNIQUE INDEX ""IX_BandwidthUsage_Account_Date"" 
                        ON mail_archiver.""BandwidthUsage"" (""MailAccountId"", ""Date"");
                    END IF;
                END $$;
            ");
            
            // Create index on Date
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM pg_indexes 
                        WHERE schemaname = 'mail_archiver' 
                        AND indexname = 'IX_BandwidthUsage_Date'
                    ) THEN
                        CREATE INDEX ""IX_BandwidthUsage_Date"" 
                        ON mail_archiver.""BandwidthUsage"" (""Date"");
                    END IF;
                END $$;
            ");
            
            // Add foreign key constraint
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.table_constraints 
                        WHERE constraint_type = 'FOREIGN KEY' 
                        AND table_schema = 'mail_archiver'
                        AND constraint_name = 'FK_BandwidthUsage_MailAccounts_MailAccountId'
                    ) THEN
                        ALTER TABLE mail_archiver.""BandwidthUsage"" 
                        ADD CONSTRAINT ""FK_BandwidthUsage_MailAccounts_MailAccountId"" 
                        FOREIGN KEY (""MailAccountId"") 
                        REFERENCES mail_archiver.""MailAccounts""(""Id"") 
                        ON DELETE CASCADE;
                    END IF;
                END $$;
            ");
            
            // Add comments
            migrationBuilder.Sql(@"
                COMMENT ON TABLE mail_archiver.""BandwidthUsage"" IS 'Tracks daily bandwidth usage per mail account for rate limit management';
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""BandwidthUsage"".""BytesDownloaded"" IS 'Total bytes downloaded on this date';
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""BandwidthUsage"".""LimitReached"" IS 'Whether the rate limit has been reached on this date';
            ");
            
            // Create SyncCheckpoints table for resumable syncing
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.tables 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'SyncCheckpoints'
                    ) THEN
                        CREATE TABLE mail_archiver.""SyncCheckpoints"" (
                            ""Id"" serial PRIMARY KEY,
                            ""MailAccountId"" integer NOT NULL,
                            ""FolderName"" text NOT NULL,
                            ""LastMessageDate"" timestamp NULL,
                            ""LastMessageId"" text NULL,
                            ""ProcessedCount"" integer NOT NULL DEFAULT 0,
                            ""CreatedAt"" timestamp NOT NULL,
                            ""UpdatedAt"" timestamp NOT NULL,
                            ""IsCompleted"" boolean NOT NULL DEFAULT false,
                            ""BytesDownloaded"" bigint NOT NULL DEFAULT 0
                        );
                    END IF;
                END $$;
            ");
            
            // Create unique index on MailAccountId and FolderName
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM pg_indexes 
                        WHERE schemaname = 'mail_archiver' 
                        AND indexname = 'IX_SyncCheckpoints_Account_Folder'
                    ) THEN
                        CREATE UNIQUE INDEX ""IX_SyncCheckpoints_Account_Folder"" 
                        ON mail_archiver.""SyncCheckpoints"" (""MailAccountId"", ""FolderName"");
                    END IF;
                END $$;
            ");
            
            // Create index on MailAccountId
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM pg_indexes 
                        WHERE schemaname = 'mail_archiver' 
                        AND indexname = 'IX_SyncCheckpoints_AccountId'
                    ) THEN
                        CREATE INDEX ""IX_SyncCheckpoints_AccountId"" 
                        ON mail_archiver.""SyncCheckpoints"" (""MailAccountId"");
                    END IF;
                END $$;
            ");
            
            // Add foreign key constraint
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 
                        FROM information_schema.table_constraints 
                        WHERE constraint_type = 'FOREIGN KEY' 
                        AND table_schema = 'mail_archiver'
                        AND constraint_name = 'FK_SyncCheckpoints_MailAccounts_MailAccountId'
                    ) THEN
                        ALTER TABLE mail_archiver.""SyncCheckpoints"" 
                        ADD CONSTRAINT ""FK_SyncCheckpoints_MailAccounts_MailAccountId"" 
                        FOREIGN KEY (""MailAccountId"") 
                        REFERENCES mail_archiver.""MailAccounts""(""Id"") 
                        ON DELETE CASCADE;
                    END IF;
                END $$;
            ");
            
            // Add comments
            migrationBuilder.Sql(@"
                COMMENT ON TABLE mail_archiver.""SyncCheckpoints"" IS 'Tracks sync progress per folder for resumable syncing when interrupted by rate limits';
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""SyncCheckpoints"".""LastMessageDate"" IS 'Date of the last successfully synced message in this folder';
            ");
            
            migrationBuilder.Sql(@"
                COMMENT ON COLUMN mail_archiver.""SyncCheckpoints"".""IsCompleted"" IS 'Whether this folder has been fully synced';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop SyncCheckpoints table
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.tables 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'SyncCheckpoints'
                    ) THEN
                        DROP TABLE mail_archiver.""SyncCheckpoints"";
                    END IF;
                END $$;
            ");
            
            // Drop BandwidthUsage table
            migrationBuilder.Sql(@"
                DO $$ 
                BEGIN
                    IF EXISTS (
                        SELECT 1 
                        FROM information_schema.tables 
                        WHERE table_schema = 'mail_archiver' 
                        AND table_name = 'BandwidthUsage'
                    ) THEN
                        DROP TABLE mail_archiver.""BandwidthUsage"";
                    END IF;
                END $$;
            ");
        }
    }
}
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2508_6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add GIN index for full-text search on ArchivedEmails
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes 
                                   WHERE schemaname = 'mail_archiver' 
                                   AND tablename = 'ArchivedEmails' 
                                   AND indexname = 'idx_archivedemails_fulltext_search') THEN
                        CREATE INDEX ""idx_archivedemails_fulltext_search"" 
                        ON mail_archiver.""ArchivedEmails"" 
                        USING GIN (to_tsvector('simple', 
                            COALESCE(""Subject"", '') || ' ' || 
                            COALESCE(""Body"", '') || ' ' || 
                            COALESCE(""From"", '') || ' ' || 
                            COALESCE(""To"", '') || ' ' || 
                            COALESCE(""Cc"", '') || ' ' || 
                            COALESCE(""Bcc"", '')));
                    END IF;
                END $$;
            ");

            // Add composite index for common search pattern: MailAccountId + SentDate
            // This will help with queries that filter by account and date range
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes 
                                   WHERE schemaname = 'mail_archiver' 
                                   AND tablename = 'ArchivedEmails' 
                                   AND indexname = 'ix_archivedemails_mailaccountid_sentdate') THEN
                        CREATE INDEX ""ix_archivedemails_mailaccountid_sentdate"" 
                        ON mail_archiver.""ArchivedEmails"" (""MailAccountId"", ""SentDate"");
                    END IF;
                END $$;
            ");

            // Add composite index for common search pattern: IsOutgoing + SentDate
            // This will help with queries that filter by direction and date range
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes 
                                   WHERE schemaname = 'mail_archiver' 
                                   AND tablename = 'ArchivedEmails' 
                                   AND indexname = 'ix_archivedemails_isoutgoing_sentdate') THEN
                        CREATE INDEX ""ix_archivedemails_isoutgoing_sentdate"" 
                        ON mail_archiver.""ArchivedEmails"" (""IsOutgoing"", ""SentDate"");
                    END IF;
                END $$;
            ");

            // Add composite index for common search pattern: MailAccountId + IsOutgoing + SentDate
            // This will help with queries that filter by account, direction, and date range
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes 
                                   WHERE schemaname = 'mail_archiver' 
                                   AND tablename = 'ArchivedEmails' 
                                   AND indexname = 'ix_archivedemails_mailaccountid_isoutgoing_sentdate') THEN
                        CREATE INDEX ""ix_archivedemails_mailaccountid_isoutgoing_sentdate"" 
                        ON mail_archiver.""ArchivedEmails"" (""MailAccountId"", ""IsOutgoing"", ""SentDate"");
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop all indexes if they exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Drop the full-text search index
                    IF EXISTS (SELECT 1 FROM pg_indexes 
                               WHERE schemaname = 'mail_archiver' 
                               AND tablename = 'ArchivedEmails' 
                               AND indexname = 'idx_archivedemails_fulltext_search') THEN
                        DROP INDEX IF EXISTS mail_archiver.""idx_archivedemails_fulltext_search"";
                    END IF;
                    
                    -- Drop composite indexes
                    IF EXISTS (SELECT 1 FROM pg_indexes 
                               WHERE schemaname = 'mail_archiver' 
                               AND tablename = 'ArchivedEmails' 
                               AND indexname = 'ix_archivedemails_mailaccountid_sentdate') THEN
                        DROP INDEX IF EXISTS mail_archiver.""ix_archivedemails_mailaccountid_sentdate"";
                    END IF;
                    
                    IF EXISTS (SELECT 1 FROM pg_indexes 
                               WHERE schemaname = 'mail_archiver' 
                               AND tablename = 'ArchivedEmails' 
                               AND indexname = 'ix_archivedemails_isoutgoing_sentdate') THEN
                        DROP INDEX IF EXISTS mail_archiver.""ix_archivedemails_isoutgoing_sentdate"";
                    END IF;
                    
                    IF EXISTS (SELECT 1 FROM pg_indexes 
                               WHERE schemaname = 'mail_archiver' 
                               AND tablename = 'ArchivedEmails' 
                               AND indexname = 'ix_archivedemails_mailaccountid_isoutgoing_sentdate') THEN
                        DROP INDEX IF EXISTS mail_archiver.""ix_archivedemails_mailaccountid_isoutgoing_sentdate"";
                    END IF;
                END $$;
            ");
        }
    }
}

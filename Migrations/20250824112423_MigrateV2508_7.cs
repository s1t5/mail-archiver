using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2508_7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create UserSessions table if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.tables 
                                   WHERE table_schema = 'mail_archiver' 
                                   AND table_name = 'UserSessions') THEN
                        CREATE TABLE mail_archiver.""UserSessions"" (
                            ""Id"" serial PRIMARY KEY,
                            ""Token"" character varying(255) NOT NULL,
                            ""Username"" character varying(50) NOT NULL,
                            ""CreatedAt"" timestamp without time zone NOT NULL,
                            ""ExpiresAt"" timestamp without time zone NULL
                        );
                    END IF;
                END $$;
            ");

            // Create indexes if they don't exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Unique index on Token
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes 
                                   WHERE schemaname = 'mail_archiver' 
                                   AND tablename = 'UserSessions' 
                                   AND indexname = 'ix_usersessions_token') THEN
                        CREATE UNIQUE INDEX ""ix_usersessions_token"" 
                        ON mail_archiver.""UserSessions"" (""Token"");
                    END IF;
                    
                    -- Index on Username
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes 
                                   WHERE schemaname = 'mail_archiver' 
                                   AND tablename = 'UserSessions' 
                                   AND indexname = 'ix_usersessions_username') THEN
                        CREATE INDEX ""ix_usersessions_username"" 
                        ON mail_archiver.""UserSessions"" (""Username"");
                    END IF;
                    
                    -- Index on ExpiresAt
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes 
                                   WHERE schemaname = 'mail_archiver' 
                                   AND tablename = 'UserSessions' 
                                   AND indexname = 'ix_usersessions_expiresat') THEN
                        CREATE INDEX ""ix_usersessions_expiresat"" 
                        ON mail_archiver.""UserSessions"" (""ExpiresAt"");
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes and table if they exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    -- Drop indexes if they exist
                    IF EXISTS (SELECT 1 FROM pg_indexes 
                               WHERE schemaname = 'mail_archiver' 
                               AND tablename = 'UserSessions' 
                               AND indexname = 'ix_usersessions_token') THEN
                        DROP INDEX IF EXISTS mail_archiver.""ix_usersessions_token"";
                    END IF;
                    
                    IF EXISTS (SELECT 1 FROM pg_indexes 
                               WHERE schemaname = 'mail_archiver' 
                               AND tablename = 'UserSessions' 
                               AND indexname = 'ix_usersessions_username') THEN
                        DROP INDEX IF EXISTS mail_archiver.""ix_usersessions_username"";
                    END IF;
                    
                    IF EXISTS (SELECT 1 FROM pg_indexes 
                               WHERE schemaname = 'mail_archiver' 
                               AND tablename = 'UserSessions' 
                               AND indexname = 'ix_usersessions_expiresat') THEN
                        DROP INDEX IF EXISTS mail_archiver.""ix_usersessions_expiresat"";
                    END IF;
                    
                    -- Drop table if it exists
                    IF EXISTS (SELECT 1 FROM information_schema.tables 
                               WHERE table_schema = 'mail_archiver' 
                               AND table_name = 'UserSessions') THEN
                        DROP TABLE IF EXISTS mail_archiver.""UserSessions"";
                    END IF;
                END $$;
            ");
        }
    }
}

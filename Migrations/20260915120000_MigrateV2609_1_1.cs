using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2609_1_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // Index for filtering the archive by folder
            // ============================================================
            // Picking a folder in the archive filters on FolderName: the folder
            // itself, plus anything below it on a path separator. FolderName had
            // no index, so the planner had to reach the page some other way. With
            // ORDER BY SentDate DESC and a LIMIT it walked the SentDate index
            // backwards and applied the folder as a row filter, betting that
            // twenty matches would turn up early. For a folder holding a handful
            // of messages that bet loses completely: it walks the whole index and
            // fetches every heap page to test a column it cannot see from there.
            //
            // Measured on an installation holding a few million rows, selecting a
            // folder with ten messages in it:
            //
            //   page query   6182 ms, 1,389,349 blocks read, all but ten rows
            //                filtered out   ->   2.5 ms, 25 blocks
            //   result count  596 ms, 394,329 blocks         ->   0.26 ms, 22 blocks
            //
            // text_pattern_ops rather than a plain btree: the three prefix
            // conditions arrive as LIKE 'folder/%', and under any collation other
            // than C a default text index cannot serve those. With the pattern
            // operator class the planner turns each one into a byte range and ORs
            // the four scans together.
            //
            // A large folder is not made worse. With around a million matching
            // rows the backward walk over SentDate stops after twenty hits and
            // stays the cheaper plan, so the planner keeps choosing it and this
            // index is never touched.
            //
            // Not CONCURRENTLY: migrations run inside a transaction, which rules
            // it out. The build takes an exclusive lock, around 30 seconds on the
            // installation measured above, while readers continue. It happens
            // during startup migration, before the application serves anything.
            //
            // Idempotent, and deliberately SQL only: the other performance indexes
            // on this table are not declared on the model either.

            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_archivedemails_foldername_pattern
                    ON mail_archiver.""ArchivedEmails"" (""FolderName"" text_pattern_ops);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS mail_archiver.ix_archivedemails_foldername_pattern;
            ");
        }
    }
}

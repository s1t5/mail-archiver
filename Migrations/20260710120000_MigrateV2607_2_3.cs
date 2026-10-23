using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2607_2_3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ============================================================
            // Full immutability protection for locked archived emails
            // ============================================================
            // Replaces the original prevent_locked_email_changes() function
            // (which only protected Subject, Body, From, To, ContentHash) with
            // a generic, column-agnostic implementation that blocks ANY change
            // to a locked row — except for:
            //   * IsLocked   — so unlocking (for retention deletion, account
            //                  deletion, and the startup DeletionPolicy
            //                  application) remains possible.
            //   * FolderName — IMAP sync updates the folder when an email is
            //                  moved server-side; this is a metadata change
            //                  and not a content manipulation, so it stays
            //                  allowed even on locked rows.
            //
            // The comparison uses to_jsonb(OLD) - excluded columns vs.
            // to_jsonb(NEW) - excluded columns. This automatically covers all
            // existing and future columns without maintaining an explicit
            // field list.
            //
            // The existing UPDATE trigger (prevent_locked_email_updates) and
            // DELETE trigger (prevent_locked_email_deletion) remain unchanged;
            // only the function they execute is replaced.

            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION mail_archiver.prevent_locked_email_changes()
                RETURNS TRIGGER AS $$
                DECLARE
                    old_data jsonb;
                    new_data jsonb;
                BEGIN
                    IF OLD.""IsLocked"" = true THEN
                        old_data := to_jsonb(OLD) - 'IsLocked' - 'FolderName';
                        new_data := to_jsonb(NEW) - 'IsLocked' - 'FolderName';
                        IF old_data IS DISTINCT FROM new_data THEN
                            RAISE EXCEPTION 'Email is locked and cannot be modified (compliance requirement)';
                        END IF;
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                COMMENT ON FUNCTION mail_archiver.prevent_locked_email_changes()
                IS 'Blocks any UPDATE of a locked ArchivedEmails row except the IsLocked and FolderName columns. Full immutability protection since v2607.2.3.';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the original 5-field implementation from MigrateV2511_1
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION mail_archiver.prevent_locked_email_changes()
                RETURNS TRIGGER AS $$
                BEGIN
                    IF OLD.""IsLocked"" = true AND (
                        NEW.""Subject"" IS DISTINCT FROM OLD.""Subject"" OR
                        NEW.""Body"" IS DISTINCT FROM OLD.""Body"" OR
                        NEW.""From"" IS DISTINCT FROM OLD.""From"" OR
                        NEW.""To"" IS DISTINCT FROM OLD.""To"" OR
                        NEW.""ContentHash"" IS DISTINCT FROM OLD.""ContentHash""
                    ) THEN
                        RAISE EXCEPTION 'Email is locked and cannot be modified (compliance requirement)';
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                COMMENT ON FUNCTION mail_archiver.prevent_locked_email_changes()
                IS 'Blocks UPDATEs to Subject, Body, From, To, ContentHash of locked ArchivedEmails rows.';
            ");
        }
    }
}
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2607_1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.tables
                        WHERE table_schema = 'mail_archiver'
                        AND table_name = 'AccountStorageCache'
                    ) THEN
                        CREATE TABLE mail_archiver.""AccountStorageCache"" (
                            ""MailAccountId"" integer NOT NULL,
                            ""MailBytes"" bigint NOT NULL DEFAULT 0,
                            ""AttachmentBytes"" bigint NOT NULL DEFAULT 0,
                            ""TotalBytes"" bigint NOT NULL DEFAULT 0,
                            ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT now(),
                            CONSTRAINT ""PK_AccountStorageCache_MailAccountId"" PRIMARY KEY (""MailAccountId""),
                            CONSTRAINT ""FK_AccountStorageCache_MailAccounts_MailAccountId""
                                FOREIGN KEY (""MailAccountId"")
                                REFERENCES mail_archiver.""MailAccounts""(""Id"")
                                ON DELETE CASCADE
                        );
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.tables
                        WHERE table_schema = 'mail_archiver'
                        AND table_name = 'AccountStorageBackfillState'
                    ) THEN
                        CREATE TABLE mail_archiver.""AccountStorageBackfillState"" (
                            ""MailAccountId"" integer NOT NULL,
                            ""Status"" varchar(10) NOT NULL DEFAULT 'Pending',
                            ""CompletedAt"" timestamp with time zone NULL,
                            CONSTRAINT ""PK_AccountStorageBackfillState_MailAccountId"" PRIMARY KEY (""MailAccountId""),
                            CONSTRAINT ""FK_AccountStorageBackfillState_MailAccounts_MailAccountId""
                                FOREIGN KEY (""MailAccountId"")
                                REFERENCES mail_archiver.""MailAccounts""(""Id"")
                                ON DELETE CASCADE
                        );
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"COMMENT ON TABLE mail_archiver.""AccountStorageCache"" IS 'Cache: Speicherverbrauch pro Account (alle Mail-Felder + Anhaenge). Wird durch Background-Service und nach Sync/Import befuellt.';");
            migrationBuilder.Sql(@"COMMENT ON COLUMN mail_archiver.""AccountStorageCache"".""MailBytes"" IS 'Speicherverbrauch aller Felder einer Mail in Bytes (pg_column_size der gesamten Zeile).';");
            migrationBuilder.Sql(@"COMMENT ON COLUMN mail_archiver.""AccountStorageCache"".""AttachmentBytes"" IS 'Logische Summe der Anhangsgroessen in Bytes (Sum EmailAttachment.Size).';");
            migrationBuilder.Sql(@"COMMENT ON COLUMN mail_archiver.""AccountStorageCache"".""TotalBytes"" IS 'Gesamtgroesse in Bytes (MailBytes + AttachmentBytes).';");
            migrationBuilder.Sql(@"COMMENT ON COLUMN mail_archiver.""AccountStorageCache"".""UpdatedAt"" IS 'Zeitpunkt der letzten Aktualisierung.';");

            migrationBuilder.Sql(@"COMMENT ON TABLE mail_archiver.""AccountStorageBackfillState"" IS 'Backfill-Status pro Account fuer die erstmalige Speicherverbrauchs-Berechnung.';");
            migrationBuilder.Sql(@"COMMENT ON COLUMN mail_archiver.""AccountStorageBackfillState"".""Status"" IS 'Pending oder Done.';");
            migrationBuilder.Sql(@"COMMENT ON COLUMN mail_archiver.""AccountStorageBackfillState"".""CompletedAt"" IS 'Zeitpunkt der Fertigstellung des Backfills fuer diesen Account.';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.tables
                        WHERE table_schema = 'mail_archiver'
                        AND table_name = 'AccountStorageBackfillState'
                    ) THEN
                        DROP TABLE mail_archiver.""AccountStorageBackfillState"";
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.tables
                        WHERE table_schema = 'mail_archiver'
                        AND table_name = 'AccountStorageCache'
                    ) THEN
                        DROP TABLE mail_archiver.""AccountStorageCache"";
                    END IF;
                END $$;
            ");
        }
    }
}

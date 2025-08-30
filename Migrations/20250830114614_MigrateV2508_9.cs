using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MailArchiver.Migrations
{
    /// <inheritdoc />
    public partial class MigrateV2508_9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSelfManager",
                schema: "mail_archiver",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsImportOnly",
                schema: "mail_archiver",
                table: "MailAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Make IMAP fields nullable for import-only accounts
            migrationBuilder.AlterColumn<string>(
                name: "ImapServer",
                schema: "mail_archiver",
                table: "MailAccounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "ImapPort",
                schema: "mail_archiver",
                table: "MailAccounts",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                schema: "mail_archiver",
                table: "MailAccounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Password",
                schema: "mail_archiver",
                table: "MailAccounts",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert IMAP fields back to not nullable
            migrationBuilder.AlterColumn<string>(
                name: "ImapServer",
                schema: "mail_archiver",
                table: "MailAccounts",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ImapPort",
                schema: "mail_archiver",
                table: "MailAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 993,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                schema: "mail_archiver",
                table: "MailAccounts",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Password",
                schema: "mail_archiver",
                table: "MailAccounts",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "IsSelfManager",
                schema: "mail_archiver",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsImportOnly",
                schema: "mail_archiver",
                table: "MailAccounts");
        }
    }
}

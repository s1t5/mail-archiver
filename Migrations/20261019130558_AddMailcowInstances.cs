using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable
namespace MailArchiver.Migrations;

public partial class AddMailcowInstances : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.CreateTable(
                name: "MailcowInstances",
                schema: "mail_archiver",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ApiUrl = table.Column<string>(type: "text", nullable: false),
                    ApiKey = table.Column<string>(type: "text", nullable: false),
                    ImapHost = table.Column<string>(type: "text", nullable: false),
                    ImapPort = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailcowInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MailcowMailboxes",
                schema: "mail_archiver",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstanceId = table.Column<int>(type: "integer", nullable: false),
                    EmailAddress = table.Column<string>(type: "text", nullable: false),
                    AppName = table.Column<string>(type: "text", nullable: false),
                    AppPasswordId = table.Column<string>(type: "text", nullable: true),
                    MailAccountId = table.Column<int>(type: "integer", nullable: true),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailcowMailboxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MailcowMailboxes_MailAccounts_MailAccountId",
                        column: x => x.MailAccountId,
                        principalSchema: "mail_archiver",
                        principalTable: "MailAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MailcowMailboxes_MailcowInstances_InstanceId",
                        column: x => x.InstanceId,
                        principalSchema: "mail_archiver",
                        principalTable: "MailcowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MailcowInstances_ApiUrl",
                schema: "mail_archiver",
                table: "MailcowInstances",
                column: "ApiUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MailcowMailboxes_InstanceId_EmailAddress",
                schema: "mail_archiver",
                table: "MailcowMailboxes",
                columns: new[] { "InstanceId", "EmailAddress" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MailcowMailboxes_MailAccountId",
                schema: "mail_archiver",
                table: "MailcowMailboxes",
                column: "MailAccountId");

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "MailcowMailboxes", schema: "mail_archiver");
        migrationBuilder.DropTable(name: "MailcowInstances", schema: "mail_archiver");
    }
}

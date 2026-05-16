using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Communications.Migrations
{
    /// <inheritdoc />
    public partial class AddManagedCommunicationBuilders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailTemplates_Key_Locale",
                schema: "communications",
                table: "EmailTemplates");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "communications",
                table: "EmailTemplates",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByAdminId",
                schema: "communications",
                table: "EmailTemplates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "communications",
                table: "EmailTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemDefault",
                schema: "communications",
                table: "EmailTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "communications",
                table: "EmailTemplates",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Version",
                schema: "communications",
                table: "EmailTemplates",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE communications."EmailTemplates"
                SET "Name" = CASE WHEN COALESCE("Name", '') = '' THEN "Key" ELSE "Name" END,
                    "Version" = CASE WHEN COALESCE("Version", '') = '' THEN '1.0.0' ELSE "Version" END,
                    "CreatedAt" = COALESCE("UpdatedAt", NOW()),
                    "IsActive" = TRUE
                WHERE COALESCE("Version", '') = '';
                """);

            migrationBuilder.CreateTable(
                name: "LegalDocumentVersions",
                schema: "communications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ChangeSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    BodyHtml = table.Column<string>(type: "text", nullable: false),
                    BodyText = table.Column<string>(type: "text", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    NotifyUsers = table.Column<bool>(type: "boolean", nullable: false),
                    BasedOnVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotificationRequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotificationRequestedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    NotificationSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalDocumentVersions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_Key_Locale_IsActive",
                schema: "communications",
                table: "EmailTemplates",
                columns: new[] { "Key", "Locale", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_Key_Locale_Version",
                schema: "communications",
                table: "EmailTemplates",
                columns: new[] { "Key", "Locale", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocumentVersions_DocumentType_IsCurrent",
                schema: "communications",
                table: "LegalDocumentVersions",
                columns: new[] { "DocumentType", "IsCurrent" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocumentVersions_DocumentType_PublishedAt",
                schema: "communications",
                table: "LegalDocumentVersions",
                columns: new[] { "DocumentType", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocumentVersions_DocumentType_Version",
                schema: "communications",
                table: "LegalDocumentVersions",
                columns: new[] { "DocumentType", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalDocumentVersions",
                schema: "communications");

            migrationBuilder.DropIndex(
                name: "IX_EmailTemplates_Key_Locale_IsActive",
                schema: "communications",
                table: "EmailTemplates");

            migrationBuilder.DropIndex(
                name: "IX_EmailTemplates_Key_Locale_Version",
                schema: "communications",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "communications",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "CreatedByAdminId",
                schema: "communications",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "communications",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "IsSystemDefault",
                schema: "communications",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "communications",
                table: "EmailTemplates");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "communications",
                table: "EmailTemplates");

            migrationBuilder.CreateIndex(
                name: "IX_EmailTemplates_Key_Locale",
                schema: "communications",
                table: "EmailTemplates",
                columns: new[] { "Key", "Locale" },
                unique: true);
        }
    }
}

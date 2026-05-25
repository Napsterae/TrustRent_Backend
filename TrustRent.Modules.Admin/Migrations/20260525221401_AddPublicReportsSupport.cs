using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicReportsSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "OpenedByUserId",
                schema: "admin",
                table: "SupportTickets",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ClientBrowser",
                schema: "admin",
                table: "SupportTickets",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientDevice",
                schema: "admin",
                table: "SupportTickets",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientOs",
                schema: "admin",
                table: "SupportTickets",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DiagnosticsConsent",
                schema: "admin",
                table: "SupportTickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                schema: "admin",
                table: "SupportTickets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MetadataJson",
                schema: "admin",
                table: "SupportTickets",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PagePath",
                schema: "admin",
                table: "SupportTickets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PageUrl",
                schema: "admin",
                table: "SupportTickets",
                type: "character varying(1200)",
                maxLength: 1200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceChannel",
                schema: "admin",
                table: "SupportTickets",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "AuthorId",
                schema: "admin",
                table: "SupportTicketMessages",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Kind",
                schema: "admin",
                table: "SupportTickets",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_PagePath",
                schema: "admin",
                table: "SupportTickets",
                column: "PagePath");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_Kind",
                schema: "admin",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_PagePath",
                schema: "admin",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "ClientBrowser",
                schema: "admin",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "ClientDevice",
                schema: "admin",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "ClientOs",
                schema: "admin",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "DiagnosticsConsent",
                schema: "admin",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "admin",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "MetadataJson",
                schema: "admin",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "PagePath",
                schema: "admin",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "PageUrl",
                schema: "admin",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "SourceChannel",
                schema: "admin",
                table: "SupportTickets");

            migrationBuilder.AlterColumn<Guid>(
                name: "OpenedByUserId",
                schema: "admin",
                table: "SupportTickets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AuthorId",
                schema: "admin",
                table: "SupportTicketMessages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}

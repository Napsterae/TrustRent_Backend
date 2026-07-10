using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddReportArchiving : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                schema: "admin",
                table: "SupportTickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByAdminId",
                schema: "admin",
                table: "SupportTickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                schema: "admin",
                table: "SupportTickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_IsArchived",
                schema: "admin",
                table: "SupportTickets",
                column: "IsArchived");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_IsArchived",
                schema: "admin",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                schema: "admin",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "ArchivedByAdminId",
                schema: "admin",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                schema: "admin",
                table: "SupportTickets");
        }
    }
}

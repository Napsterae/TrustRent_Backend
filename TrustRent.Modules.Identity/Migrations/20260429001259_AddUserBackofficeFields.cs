using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddUserBackofficeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AnonymizedAt",
                schema: "identity",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AnonymizedByAdminId",
                schema: "identity",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuspended",
                schema: "identity",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "SuspendedAt",
                schema: "identity",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SuspendedByAdminId",
                schema: "identity",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SuspendedReason",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnonymizedAt",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AnonymizedByAdminId",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsSuspended",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SuspendedAt",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SuspendedByAdminId",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SuspendedReason",
                schema: "identity",
                table: "Users");
        }
    }
}

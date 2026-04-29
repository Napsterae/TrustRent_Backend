using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyModerationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BlockReason",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BlockedAt",
                schema: "catalog",
                table: "Properties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BlockedByAdminId",
                schema: "catalog",
                table: "Properties",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBlocked",
                schema: "catalog",
                table: "Properties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                schema: "catalog",
                table: "Properties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ModeratedAt",
                schema: "catalog",
                table: "Properties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModeratedByAdminId",
                schema: "catalog",
                table: "Properties",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationReason",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationStatus",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockReason",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "BlockedAt",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "BlockedByAdminId",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "IsBlocked",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "ModeratedAt",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "ModeratedByAdminId",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "ModerationReason",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "ModerationStatus",
                schema: "catalog",
                table: "Properties");
        }
    }
}

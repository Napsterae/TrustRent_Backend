using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppLoginAndNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailNotificationsEnabled",
                schema: "identity",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPhoneNumberVerified",
                schema: "identity",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PhoneNumberVerifiedAt",
                schema: "identity",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WhatsAppNotificationsEnabled",
                schema: "identity",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "WhatsAppOneTimeCodes",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvalidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    RequestedFromIp = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RequestedUserAgent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppOneTimeCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneNumber",
                schema: "identity",
                table: "Users",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppOneTimeCodes_ExpiresAt",
                schema: "identity",
                table: "WhatsAppOneTimeCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppOneTimeCodes_PhoneNumber_Purpose_RequestedAt",
                schema: "identity",
                table: "WhatsAppOneTimeCodes",
                columns: new[] { "PhoneNumber", "Purpose", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppOneTimeCodes_UserId_Purpose_RequestedAt",
                schema: "identity",
                table: "WhatsAppOneTimeCodes",
                columns: new[] { "UserId", "Purpose", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppOneTimeCodes",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_Users_PhoneNumber",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailNotificationsEnabled",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsPhoneNumberVerified",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhoneNumberVerifiedAt",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WhatsAppNotificationsEnabled",
                schema: "identity",
                table: "Users");
        }
    }
}

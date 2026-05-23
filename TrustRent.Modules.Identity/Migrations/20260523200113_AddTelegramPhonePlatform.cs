using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramPhonePlatform : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneContactPlatform",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "telegram");

            migrationBuilder.AddColumn<string>(
                name: "TelegramChatId",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TelegramLinkedAt",
                schema: "identity",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramPendingExpectedPhoneNumber",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramPendingVerificationError",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TelegramPendingVerificationExpiresAt",
                schema: "identity",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramPendingVerificationToken",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelegramUsername",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE identity.""Users""
                SET ""PhoneContactPlatform"" = CASE
                    WHEN COALESCE(""IsPhoneNumberVerified"", FALSE) = TRUE
                         OR COALESCE(""WhatsAppNotificationsEnabled"", FALSE) = TRUE
                    THEN 'whatsapp'
                    ELSE 'telegram'
                END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneContactPlatform",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramChatId",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramLinkedAt",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramPendingExpectedPhoneNumber",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramPendingVerificationError",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramPendingVerificationExpiresAt",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramPendingVerificationToken",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TelegramUsername",
                schema: "identity",
                table: "Users");
        }
    }
}

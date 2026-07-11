using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeV1ToV2EncryptedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_CitizenCardNumber",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PhoneNumber",
                schema: "identity",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "CitizenCardNumberBlindIndex",
                schema: "identity",
                table: "Users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumberBlindIndex",
                schema: "identity",
                table: "Users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_CitizenCardNumberBlindIndex",
                schema: "identity",
                table: "Users",
                column: "CitizenCardNumberBlindIndex",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneNumberBlindIndex",
                schema: "identity",
                table: "Users",
                column: "PhoneNumberBlindIndex",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_CitizenCardNumberBlindIndex",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PhoneNumberBlindIndex",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CitizenCardNumberBlindIndex",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PhoneNumberBlindIndex",
                schema: "identity",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CitizenCardNumber",
                schema: "identity",
                table: "Users",
                column: "CitizenCardNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PhoneNumber",
                schema: "identity",
                table: "Users",
                column: "PhoneNumber",
                unique: true);
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddFullUserProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CitizenCardNumber",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalCode",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfilePictureUrl",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "Address", "CitizenCardNumber", "CreatedAt", "PasswordHash", "PostalCode", "ProfilePictureUrl" },
                values: new object[] { null, null, new DateTime(2026, 3, 26, 0, 36, 26, 784, DateTimeKind.Utc).AddTicks(7013), "$2a$11$Rr.lcoUy9NH8kj03F28D2.oX7Qlu.rTQThZZWGJ.JQqTDlPeOzb4i", null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Users_CitizenCardNumber",
                schema: "identity",
                table: "Users",
                column: "CitizenCardNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Nif",
                schema: "identity",
                table: "Users",
                column: "Nif",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_CitizenCardNumber",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Nif",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Address",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CitizenCardNumber",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PostalCode",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfilePictureUrl",
                schema: "identity",
                table: "Users");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2026, 3, 25, 23, 48, 44, 468, DateTimeKind.Utc).AddTicks(1458), "$2a$11$vY7EifkEvrGkmupUvGM7tukGNqxophQZMlw3Eqg4svYTTpiaKLEsG" });
        }
    }
}

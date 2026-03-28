using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsIdentityVerified",
                schema: "identity",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsNoDebtVerified",
                schema: "identity",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedAt", "IsIdentityVerified", "IsNoDebtVerified", "PasswordHash" },
                values: new object[] { new DateTime(2026, 3, 25, 23, 48, 44, 468, DateTimeKind.Utc).AddTicks(1458), false, false, "$2a$11$vY7EifkEvrGkmupUvGM7tukGNqxophQZMlw3Eqg4svYTTpiaKLEsG" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsIdentityVerified",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsNoDebtVerified",
                schema: "identity",
                table: "Users");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2026, 3, 25, 23, 22, 50, 914, DateTimeKind.Utc).AddTicks(3130), "$2a$11$09TGTFJee0tLcl4Yam2hFe.AYjgS1lGvpDqRbRXLOdElgCDdsgucO" });
        }
    }
}

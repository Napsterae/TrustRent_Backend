using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddNifToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Nif",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedAt", "Nif", "PasswordHash" },
                values: new object[] { new DateTime(2026, 3, 25, 23, 22, 50, 914, DateTimeKind.Utc).AddTicks(3130), null, "$2a$11$09TGTFJee0tLcl4Yam2hFe.AYjgS1lGvpDqRbRXLOdElgCDdsgucO" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nif",
                schema: "identity",
                table: "Users");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2026, 3, 24, 23, 54, 2, 76, DateTimeKind.Utc).AddTicks(8178), "$2a$11$7zjt/0dawmPEwYhBdRugbOBay0Sd4rzwK1pcIHB9TBjINkWYW4B9." });
        }
    }
}

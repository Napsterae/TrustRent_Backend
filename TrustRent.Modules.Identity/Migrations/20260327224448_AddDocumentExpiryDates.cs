using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentExpiryDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "IdentityExpiryDate",
                schema: "identity",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NoDebtExpiryDate",
                schema: "identity",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedAt", "IdentityExpiryDate", "NoDebtExpiryDate", "PasswordHash" },
                values: new object[] { new DateTime(2026, 3, 27, 22, 44, 47, 375, DateTimeKind.Utc).AddTicks(5920), null, null, "$2a$11$q8WUWA0FK.qL9f9prW37FulTljZgR/Wdm3dDa2oBYhtsJ1CTUhR.i" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdentityExpiryDate",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NoDebtExpiryDate",
                schema: "identity",
                table: "Users");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "CreatedAt", "PasswordHash" },
                values: new object[] { new DateTime(2026, 3, 26, 0, 36, 26, 784, DateTimeKind.Utc).AddTicks(7013), "$2a$11$Rr.lcoUy9NH8kj03F28D2.oX7Qlu.rTQThZZWGJ.JQqTDlPeOzb4i" });
        }
    }
}

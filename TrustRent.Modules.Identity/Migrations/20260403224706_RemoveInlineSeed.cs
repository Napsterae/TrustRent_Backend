using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class RemoveInlineSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "identity",
                table: "Users",
                columns: new[] { "Id", "Address", "CitizenCardNumber", "CreatedAt", "Email", "IdentityExpiryDate", "IsIdentityVerified", "IsNoDebtVerified", "Name", "Nif", "NoDebtExpiryDate", "PasswordHash", "PostalCode", "ProfilePictureUrl", "TrustScore" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), null, null, new DateTime(2026, 3, 27, 22, 44, 47, 375, DateTimeKind.Utc).AddTicks(5920), "joao.silva@email.pt", null, false, false, "João Silva", null, null, "$2a$11$q8WUWA0FK.qL9f9prW37FulTljZgR/Wdm3dDa2oBYhtsJ1CTUhR.i", null, null, 85 });
        }
    }
}

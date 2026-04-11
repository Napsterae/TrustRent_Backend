using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddSequentialSignatureFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LandlordSignatureCertSubject",
                schema: "catalog",
                table: "Leases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LandlordSignatureVerified",
                schema: "catalog",
                table: "Leases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LandlordSignedFilePath",
                schema: "catalog",
                table: "Leases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantSignatureCertSubject",
                schema: "catalog",
                table: "Leases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TenantSignatureVerified",
                schema: "catalog",
                table: "Leases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TenantSignedFilePath",
                schema: "catalog",
                table: "Leases",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LandlordSignatureCertSubject",
                schema: "catalog",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "LandlordSignatureVerified",
                schema: "catalog",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "LandlordSignedFilePath",
                schema: "catalog",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "TenantSignatureCertSubject",
                schema: "catalog",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "TenantSignatureVerified",
                schema: "catalog",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "TenantSignedFilePath",
                schema: "catalog",
                table: "Leases");
        }
    }
}

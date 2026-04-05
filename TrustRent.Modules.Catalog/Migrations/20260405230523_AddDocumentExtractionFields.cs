using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentExtractionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ParishConcelho",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PermanentCertNumber",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PermanentCertOffice",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsageLicenseDate",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsageLicenseIssuer",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsageLicenseNumber",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ParishConcelho",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "PermanentCertNumber",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "PermanentCertOffice",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "UsageLicenseDate",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "UsageLicenseIssuer",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "UsageLicenseNumber",
                schema: "catalog",
                table: "Properties");
        }
    }
}

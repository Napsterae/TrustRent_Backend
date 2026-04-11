using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddContractFileHashes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractFileHash",
                schema: "catalog",
                table: "Leases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LandlordSignedFileHash",
                schema: "catalog",
                table: "Leases",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContractFileHash",
                schema: "catalog",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "LandlordSignedFileHash",
                schema: "catalog",
                table: "Leases");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAddressFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "City",
                schema: "catalog",
                table: "Properties",
                newName: "Parish");

            migrationBuilder.AddColumn<string>(
                name: "District",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DoorNumber",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Municipality",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "District",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "DoorNumber",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Municipality",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.RenameColumn(
                name: "Parish",
                schema: "catalog",
                table: "Properties",
                newName: "City");
        }
    }
}

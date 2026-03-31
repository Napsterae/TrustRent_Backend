using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddPropertyStatusToggles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsAvailable",
                schema: "catalog",
                table: "Properties",
                newName: "IsUnderMaintenance");

            migrationBuilder.AddColumn<bool>(
                name: "IsPublic",
                schema: "catalog",
                table: "Properties",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPublic",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.RenameColumn(
                name: "IsUnderMaintenance",
                schema: "catalog",
                table: "Properties",
                newName: "IsAvailable");
        }
    }
}

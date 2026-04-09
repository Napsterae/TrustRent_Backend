using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodicityAndLeaseRegime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowsRenewal",
                schema: "catalog",
                table: "Properties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LeaseRegime",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NonPermanentReason",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PropertyPeriodicities",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DurationMonths = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyPeriodicities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyPeriodicities_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalSchema: "catalog",
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyPeriodicities_PropertyId",
                schema: "catalog",
                table: "PropertyPeriodicities",
                column: "PropertyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyPeriodicities",
                schema: "catalog");

            migrationBuilder.DropColumn(
                name: "AllowsRenewal",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "LeaseRegime",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "NonPermanentReason",
                schema: "catalog",
                table: "Properties");
        }
    }
}

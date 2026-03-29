using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class InitCatalogModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "Properties",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LandlordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    PropertyType = table.Column<string>(type: "text", nullable: false),
                    Typology = table.Column<string>(type: "text", nullable: false),
                    Area = table.Column<decimal>(type: "numeric", nullable: false),
                    Rooms = table.Column<int>(type: "integer", nullable: false),
                    Bathrooms = table.Column<int>(type: "integer", nullable: false),
                    Floor = table.Column<string>(type: "text", nullable: false),
                    HasElevator = table.Column<bool>(type: "boolean", nullable: false),
                    HasAirConditioning = table.Column<bool>(type: "boolean", nullable: false),
                    HasGarage = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsPets = table.Column<bool>(type: "boolean", nullable: false),
                    IsFurnished = table.Column<bool>(type: "boolean", nullable: false),
                    FurnishedDescription = table.Column<string>(type: "text", nullable: true),
                    Street = table.Column<string>(type: "text", nullable: false),
                    PostalCode = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Properties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PropertyDocuments",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyDocuments_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalSchema: "catalog",
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PropertyImages",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    IsMain = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropertyImages_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalSchema: "catalog",
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDocuments_PropertyId",
                schema: "catalog",
                table: "PropertyDocuments",
                column: "PropertyId");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyImages_PropertyId",
                schema: "catalog",
                table: "PropertyImages",
                column: "PropertyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyDocuments",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "PropertyImages",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Properties",
                schema: "catalog");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddAmenitiesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Amenities",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IconName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Amenities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PropertyAmenities",
                schema: "catalog",
                columns: table => new
                {
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmenityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropertyAmenities", x => new { x.PropertyId, x.AmenityId });
                    table.ForeignKey(
                        name: "FK_PropertyAmenities_Amenities_AmenityId",
                        column: x => x.AmenityId,
                        principalSchema: "catalog",
                        principalTable: "Amenities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PropertyAmenities_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalSchema: "catalog",
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "catalog",
                table: "Amenities",
                columns: new[] { "Id", "Category", "IconName", "Name" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000001"), "Básico", "Wifi", "Wifi" },
                    { new Guid("a0000000-0000-0000-0000-000000000002"), "Básico", "Utensils", "Cozinha Equipada" },
                    { new Guid("a0000000-0000-0000-0000-000000000003"), "Básico", "WashingMachine", "Máquina de Lavar" },
                    { new Guid("a0000000-0000-0000-0000-000000000004"), "Básico", "Iron", "Ferro de Engomar" },
                    { new Guid("a0000000-0000-0000-0000-000000000005"), "Conforto", "Wind", "Ar Condicionado" },
                    { new Guid("a0000000-0000-0000-0000-000000000006"), "Conforto", "Thermometer", "Aquecimento Central" },
                    { new Guid("a0000000-0000-0000-0000-000000000007"), "Conforto", "Tv", "Televisão" },
                    { new Guid("a0000000-0000-0000-0000-000000000008"), "Conforto", "Baby", "Berço" },
                    { new Guid("a0000000-0000-0000-0000-000000000009"), "Lazer", "Waves", "Piscina" },
                    { new Guid("a0000000-0000-0000-0000-00000000000a"), "Lazer", "Bath", "Jacuzzi" },
                    { new Guid("a0000000-0000-0000-0000-00000000000b"), "Lazer", "Dumbbell", "Ginásio" },
                    { new Guid("a0000000-0000-0000-0000-00000000000c"), "Lazer", "Flame", "Churrasqueira" },
                    { new Guid("a0000000-0000-0000-0000-00000000000d"), "Segurança", "FireExtinguisher", "Extintor" },
                    { new Guid("a0000000-0000-0000-0000-00000000000e"), "Segurança", "Siren", "Detetor de Fumo" },
                    { new Guid("a0000000-0000-0000-0000-00000000000f"), "Segurança", "ShieldAlert", "Alarme" },
                    { new Guid("a0000000-0000-0000-0000-000000000010"), "Extra", "Dog", "Aceita Animais" },
                    { new Guid("a0000000-0000-0000-0000-000000000011"), "Extra", "ShoppingCart", "Próximo de Supermercado" },
                    { new Guid("a0000000-0000-0000-0000-000000000012"), "Extra", "Bus", "Próximo de Transporte" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropertyAmenities_AmenityId",
                schema: "catalog",
                table: "PropertyAmenities",
                column: "AmenityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyAmenities",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Amenities",
                schema: "catalog");
        }
    }
}

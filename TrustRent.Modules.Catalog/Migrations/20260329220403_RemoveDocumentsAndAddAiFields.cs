using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDocumentsAndAddAiFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PropertyDocuments",
                schema: "catalog");

            migrationBuilder.AddColumn<string>(
                name: "AtRegistrationNumber",
                schema: "catalog",
                table: "Properties",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EnergyCertificateExpiryDate",
                schema: "catalog",
                table: "Properties",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnergyCertificateNumber",
                schema: "catalog",
                table: "Properties",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EnergyClass",
                schema: "catalog",
                table: "Properties",
                type: "character varying(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MatrixArticle",
                schema: "catalog",
                table: "Properties",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PropertyFraction",
                schema: "catalog",
                table: "Properties",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtRegistrationNumber",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "EnergyCertificateExpiryDate",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "EnergyCertificateNumber",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "EnergyClass",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "MatrixArticle",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "PropertyFraction",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.CreateTable(
                name: "PropertyDocuments",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_PropertyDocuments_PropertyId",
                schema: "catalog",
                table: "PropertyDocuments",
                column: "PropertyId");
        }
    }
}

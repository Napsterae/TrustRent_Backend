using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddEmploymentDetailsToApplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmployerName",
                schema: "catalog",
                table: "Applications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployerNif",
                schema: "catalog",
                table: "Applications",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmploymentStartDate",
                schema: "catalog",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EmploymentType",
                schema: "catalog",
                table: "Applications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IncomeValidationMethod",
                schema: "catalog",
                table: "Applications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayslipsProvidedCount",
                schema: "catalog",
                table: "Applications",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployerName",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "EmployerNif",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "EmploymentStartDate",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "EmploymentType",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "IncomeValidationMethod",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "PayslipsProvidedCount",
                schema: "catalog",
                table: "Applications");
        }
    }
}

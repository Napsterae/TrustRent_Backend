using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddDepositAndExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CondominiumFeesPaidBy",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Deposit",
                schema: "catalog",
                table: "Properties",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ElectricityPaidBy",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GasPaidBy",
                schema: "catalog",
                table: "Properties",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WaterPaidBy",
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
                name: "CondominiumFeesPaidBy",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "Deposit",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "ElectricityPaidBy",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "GasPaidBy",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "WaterPaidBy",
                schema: "catalog",
                table: "Properties");
        }
    }
}

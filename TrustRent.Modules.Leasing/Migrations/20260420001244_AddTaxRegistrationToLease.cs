using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxRegistrationToLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRegisteredWithTaxAuthority",
                schema: "leasing",
                table: "Leases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "TaxRegistrationDate",
                schema: "leasing",
                table: "Leases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxRegistrationReference",
                schema: "leasing",
                table: "Leases",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRegisteredWithTaxAuthority",
                schema: "leasing",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "TaxRegistrationDate",
                schema: "leasing",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "TaxRegistrationReference",
                schema: "leasing",
                table: "Leases");
        }
    }
}

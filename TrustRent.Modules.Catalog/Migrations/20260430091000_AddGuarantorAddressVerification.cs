using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TrustRent.Modules.Catalog.Contracts.Database;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    [DbContext(typeof(CatalogDbContext))]
    [Migration("20260430091000_AddGuarantorAddressVerification")]
    public partial class AddGuarantorAddressVerification : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuestAddress",
                schema: "catalog",
                table: "Guarantors",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestPostalCode",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAddressVerified",
                schema: "catalog",
                table: "Guarantors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AddressVerifiedAt",
                schema: "catalog",
                table: "Guarantors",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "GuestAddress", schema: "catalog", table: "Guarantors");
            migrationBuilder.DropColumn(name: "GuestPostalCode", schema: "catalog", table: "Guarantors");
            migrationBuilder.DropColumn(name: "IsAddressVerified", schema: "catalog", table: "Guarantors");
            migrationBuilder.DropColumn(name: "AddressVerifiedAt", schema: "catalog", table: "Guarantors");
        }
    }
}
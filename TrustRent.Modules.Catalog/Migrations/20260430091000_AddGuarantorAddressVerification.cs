using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
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

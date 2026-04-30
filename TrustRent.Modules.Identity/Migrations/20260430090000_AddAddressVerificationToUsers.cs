using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TrustRent.Modules.Identity.Contracts.Database;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20260430090000_AddAddressVerificationToUsers")]
    public partial class AddAddressVerificationToUsers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAddressVerified",
                schema: "identity",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AddressVerifiedAt",
                schema: "identity",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAddressVerified",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AddressVerifiedAt",
                schema: "identity",
                table: "Users");
        }
    }
}
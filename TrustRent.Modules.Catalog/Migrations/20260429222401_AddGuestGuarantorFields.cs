using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestGuarantorFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "catalog",
                table: "Guarantors",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "CreatedFromIp",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestAccessToken",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE catalog.\"Guarantors\" SET \"GuestAccessToken\" = \"Id\"::text WHERE \"GuestAccessToken\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "GuestAccessToken",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GuestName",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestPhoneNumber",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GuestTokenIssuedAt",
                schema: "catalog",
                table: "Guarantors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GuestTokenLastUsedAt",
                schema: "catalog",
                table: "Guarantors",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guarantors_GuestAccessToken",
                schema: "catalog",
                table: "Guarantors",
                column: "GuestAccessToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guarantors_GuestEmail_InviteStatus",
                schema: "catalog",
                table: "Guarantors",
                columns: new[] { "GuestEmail", "InviteStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Guarantors_GuestAccessToken",
                schema: "catalog",
                table: "Guarantors");

            migrationBuilder.DropIndex(
                name: "IX_Guarantors_GuestEmail_InviteStatus",
                schema: "catalog",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "CreatedFromIp",
                schema: "catalog",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "GuestAccessToken",
                schema: "catalog",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "GuestEmail",
                schema: "catalog",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "GuestName",
                schema: "catalog",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "GuestPhoneNumber",
                schema: "catalog",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "GuestTokenIssuedAt",
                schema: "catalog",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "GuestTokenLastUsedAt",
                schema: "catalog",
                table: "Guarantors");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "catalog",
                table: "Guarantors",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}

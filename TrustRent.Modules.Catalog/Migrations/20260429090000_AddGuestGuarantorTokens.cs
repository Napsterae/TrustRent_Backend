using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    public partial class AddGuestGuarantorTokens : Migration
    {
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

            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

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

            migrationBuilder.Sql("""
                UPDATE catalog."Guarantors"
                SET "GuestEmail" = COALESCE("GuestEmail", 'fiador-' || "Id"::text || '@guest.trustrent.local'),
                    "GuestAccessToken" = COALESCE("GuestAccessToken", replace(replace(md5(random()::text || clock_timestamp()::text || "Id"::text), '+', '-'), '/', '_')),
                    "GuestTokenIssuedAt" = COALESCE("GuestTokenIssuedAt", "CreatedAt")
                WHERE "GuestEmail" IS NULL OR "GuestAccessToken" IS NULL OR "GuestTokenIssuedAt" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "GuestEmail",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);

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

            migrationBuilder.DropColumn(name: "CreatedFromIp", schema: "catalog", table: "Guarantors");
            migrationBuilder.DropColumn(name: "GuestAccessToken", schema: "catalog", table: "Guarantors");
            migrationBuilder.DropColumn(name: "GuestEmail", schema: "catalog", table: "Guarantors");
            migrationBuilder.DropColumn(name: "GuestName", schema: "catalog", table: "Guarantors");
            migrationBuilder.DropColumn(name: "GuestPhoneNumber", schema: "catalog", table: "Guarantors");
            migrationBuilder.DropColumn(name: "GuestTokenIssuedAt", schema: "catalog", table: "Guarantors");
            migrationBuilder.DropColumn(name: "GuestTokenLastUsedAt", schema: "catalog", table: "Guarantors");

            migrationBuilder.Sql("""
                UPDATE catalog."Guarantors"
                SET "UserId" = '00000000-0000-0000-0000-000000000000'
                WHERE "UserId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                schema: "catalog",
                table: "Guarantors",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
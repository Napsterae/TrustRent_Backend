using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class EncryptGuarantorPiiV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Guarantors_GuestEmail_InviteStatus",
                schema: "catalog",
                table: "Guarantors");

            migrationBuilder.AlterColumn<string>(
                name: "GuestPhoneNumber",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GuestName",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GuestEmail",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);

            migrationBuilder.AddColumn<string>(
                name: "GuestEmailBlindIndex",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guarantors_GuestEmailBlindIndex",
                schema: "catalog",
                table: "Guarantors",
                column: "GuestEmailBlindIndex");

            migrationBuilder.CreateIndex(
                name: "IX_Guarantors_GuestEmailBlindIndex_InviteStatus",
                schema: "catalog",
                table: "Guarantors",
                columns: new[] { "GuestEmailBlindIndex", "InviteStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Guarantors_GuestEmailBlindIndex",
                schema: "catalog",
                table: "Guarantors");

            migrationBuilder.DropIndex(
                name: "IX_Guarantors_GuestEmailBlindIndex_InviteStatus",
                schema: "catalog",
                table: "Guarantors");

            migrationBuilder.DropColumn(
                name: "GuestEmailBlindIndex",
                schema: "catalog",
                table: "Guarantors");

            migrationBuilder.AlterColumn<string>(
                name: "GuestPhoneNumber",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(400)",
                oldMaxLength: 400,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GuestName",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(400)",
                oldMaxLength: 400,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "GuestEmail",
                schema: "catalog",
                table: "Guarantors",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(400)",
                oldMaxLength: 400);

            migrationBuilder.CreateIndex(
                name: "IX_Guarantors_GuestEmail_InviteStatus",
                schema: "catalog",
                table: "Guarantors",
                columns: new[] { "GuestEmail", "InviteStatus" });
        }
    }
}

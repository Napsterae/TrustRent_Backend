using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class EncryptHighValuePiiV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WhatsAppOneTimeCodes_PhoneNumber_Purpose_RequestedAt",
                schema: "identity",
                table: "WhatsAppOneTimeCodes");

            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Nif",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_EmailLoginCodes_Email_RequestedAt",
                schema: "identity",
                table: "EmailLoginCodes");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                schema: "identity",
                table: "WhatsAppOneTimeCodes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumberBlindIndex",
                schema: "identity",
                table: "WhatsAppOneTimeCodes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmailBlindIndex",
                schema: "identity",
                table: "Users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NifBlindIndex",
                schema: "identity",
                table: "Users",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "identity",
                table: "EmailLoginCodes",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);

            migrationBuilder.AddColumn<string>(
                name: "EmailBlindIndex",
                schema: "identity",
                table: "EmailLoginCodes",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppOneTimeCodes_PhoneNumberBlindIndex_Purpose_UserId_R~",
                schema: "identity",
                table: "WhatsAppOneTimeCodes",
                columns: new[] { "PhoneNumberBlindIndex", "Purpose", "UserId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_EmailBlindIndex",
                schema: "identity",
                table: "Users",
                column: "EmailBlindIndex",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NifBlindIndex",
                schema: "identity",
                table: "Users",
                column: "NifBlindIndex",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailLoginCodes_EmailBlindIndex_RequestedAt",
                schema: "identity",
                table: "EmailLoginCodes",
                columns: new[] { "EmailBlindIndex", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WhatsAppOneTimeCodes_PhoneNumberBlindIndex_Purpose_UserId_R~",
                schema: "identity",
                table: "WhatsAppOneTimeCodes");

            migrationBuilder.DropIndex(
                name: "IX_Users_EmailBlindIndex",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_NifBlindIndex",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_EmailLoginCodes_EmailBlindIndex_RequestedAt",
                schema: "identity",
                table: "EmailLoginCodes");

            migrationBuilder.DropColumn(
                name: "PhoneNumberBlindIndex",
                schema: "identity",
                table: "WhatsAppOneTimeCodes");

            migrationBuilder.DropColumn(
                name: "EmailBlindIndex",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NifBlindIndex",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailBlindIndex",
                schema: "identity",
                table: "EmailLoginCodes");

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                schema: "identity",
                table: "WhatsAppOneTimeCodes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "identity",
                table: "EmailLoginCodes",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppOneTimeCodes_PhoneNumber_Purpose_RequestedAt",
                schema: "identity",
                table: "WhatsAppOneTimeCodes",
                columns: new[] { "PhoneNumber", "Purpose", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "identity",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Nif",
                schema: "identity",
                table: "Users",
                column: "Nif",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailLoginCodes_Email_RequestedAt",
                schema: "identity",
                table: "EmailLoginCodes",
                columns: new[] { "Email", "RequestedAt" });
        }
    }
}

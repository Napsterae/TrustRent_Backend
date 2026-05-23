using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingPhoneVerificationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingPhoneContactPlatform",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingPhoneCountryCode",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingPhoneNumber",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingPhoneContactPlatform",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PendingPhoneCountryCode",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PendingPhoneNumber",
                schema: "identity",
                table: "Users");
        }
    }
}

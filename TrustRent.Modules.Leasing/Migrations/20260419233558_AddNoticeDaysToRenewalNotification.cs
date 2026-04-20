using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class AddNoticeDaysToRenewalNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LandlordNoticeDays",
                schema: "leasing",
                table: "LeaseRenewalNotifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantNoticeDays",
                schema: "leasing",
                table: "LeaseRenewalNotifications",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LandlordNoticeDays",
                schema: "leasing",
                table: "LeaseRenewalNotifications");

            migrationBuilder.DropColumn(
                name: "TenantNoticeDays",
                schema: "leasing",
                table: "LeaseRenewalNotifications");
        }
    }
}

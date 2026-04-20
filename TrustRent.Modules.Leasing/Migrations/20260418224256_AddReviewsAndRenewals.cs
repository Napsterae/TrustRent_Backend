using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewsAndRenewals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "CardLast4",
                schema: "leasing",
                table: "TenantPaymentMethods",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(4)",
                oldMaxLength: 4);

            migrationBuilder.AlterColumn<int>(
                name: "CardExpYear",
                schema: "leasing",
                table: "TenantPaymentMethods",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "CardExpMonth",
                schema: "leasing",
                table: "TenantPaymentMethods",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "CardBrand",
                schema: "leasing",
                table: "TenantPaymentMethods",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                schema: "leasing",
                table: "TenantPaymentMethods",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                schema: "leasing",
                table: "TenantPaymentMethods",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "card");

            migrationBuilder.CreateTable(
                name: "LeaseRenewalNotifications",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeadlineDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LandlordResponse = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LandlordRespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantResponse = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    TenantRespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Processed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseRenewalNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PairId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaseRenewalNotifications_LeaseId",
                schema: "leasing",
                table: "LeaseRenewalNotifications",
                column: "LeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseRenewalNotifications_Processed",
                schema: "leasing",
                table: "LeaseRenewalNotifications",
                column: "Processed");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_LeaseId",
                schema: "leasing",
                table: "Reviews",
                column: "LeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_PairId",
                schema: "leasing",
                table: "Reviews",
                column: "PairId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewedUserId",
                schema: "leasing",
                table: "Reviews",
                column: "ReviewedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ReviewerId",
                schema: "leasing",
                table: "Reviews",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Status",
                schema: "leasing",
                table: "Reviews",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_TicketId",
                schema: "leasing",
                table: "Reviews",
                column: "TicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaseRenewalNotifications",
                schema: "leasing");

            migrationBuilder.DropTable(
                name: "Reviews",
                schema: "leasing");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                schema: "leasing",
                table: "TenantPaymentMethods");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "leasing",
                table: "TenantPaymentMethods");

            migrationBuilder.AlterColumn<string>(
                name: "CardLast4",
                schema: "leasing",
                table: "TenantPaymentMethods",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(4)",
                oldMaxLength: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CardExpYear",
                schema: "leasing",
                table: "TenantPaymentMethods",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CardExpMonth",
                schema: "leasing",
                table: "TenantPaymentMethods",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CardBrand",
                schema: "leasing",
                table: "TenantPaymentMethods",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}

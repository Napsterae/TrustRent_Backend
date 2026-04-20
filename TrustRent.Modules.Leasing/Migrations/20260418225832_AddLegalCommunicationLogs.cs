using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalCommunicationLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LandlordResponseIpAddress",
                schema: "leasing",
                table: "LeaseRenewalNotifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantResponseIpAddress",
                schema: "leasing",
                table: "LeaseRenewalNotifications",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LegalCommunicationLogs",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunicationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SenderIpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    SenderUserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ViewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ViewerIpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    ViewerUserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgerIpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    NotificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    RenewalNotificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    EmailSent = table.Column<bool>(type: "boolean", nullable: false),
                    EmailSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmailRecipientAddress = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalCommunicationLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalCommunicationLogs_CommunicationType",
                schema: "leasing",
                table: "LegalCommunicationLogs",
                column: "CommunicationType");

            migrationBuilder.CreateIndex(
                name: "IX_LegalCommunicationLogs_LeaseId",
                schema: "leasing",
                table: "LegalCommunicationLogs",
                column: "LeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalCommunicationLogs_RecipientId",
                schema: "leasing",
                table: "LegalCommunicationLogs",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalCommunicationLogs_SenderId",
                schema: "leasing",
                table: "LegalCommunicationLogs",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_LegalCommunicationLogs_SentAt",
                schema: "leasing",
                table: "LegalCommunicationLogs",
                column: "SentAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalCommunicationLogs",
                schema: "leasing");

            migrationBuilder.DropColumn(
                name: "LandlordResponseIpAddress",
                schema: "leasing",
                table: "LeaseRenewalNotifications");

            migrationBuilder.DropColumn(
                name: "TenantResponseIpAddress",
                schema: "leasing",
                table: "LeaseRenewalNotifications");
        }
    }
}

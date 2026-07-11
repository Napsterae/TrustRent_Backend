using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Admin.Migrations
{
    /// <inheritdoc />
    public partial class AddConsentRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsentRecords",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionId = table.Column<string>(type: "text", nullable: true),
                    ConsentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PurposesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Mechanism = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    BannerVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PrivacyPolicyVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsentRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_CreatedAt",
                schema: "admin",
                table: "ConsentRecords",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_OpenedByUserId",
                schema: "admin",
                table: "ConsentRecords",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsentRecords_SessionId",
                schema: "admin",
                table: "ConsentRecords",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsentRecords",
                schema: "admin");
        }
    }
}

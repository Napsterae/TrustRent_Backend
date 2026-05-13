using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailLoginCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailLoginCodes",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvalidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    RequestedFromIp = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RequestedUserAgent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailLoginCodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailLoginCodes_Email_RequestedAt",
                schema: "identity",
                table: "EmailLoginCodes",
                columns: new[] { "Email", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailLoginCodes_ExpiresAt",
                schema: "identity",
                table: "EmailLoginCodes",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailLoginCodes",
                schema: "identity");
        }
    }
}

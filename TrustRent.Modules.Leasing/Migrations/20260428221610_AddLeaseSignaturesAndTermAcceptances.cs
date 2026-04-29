using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseSignaturesAndTermAcceptances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CoTenantId",
                schema: "leasing",
                table: "Leases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GuarantorRecordId",
                schema: "leasing",
                table: "Leases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GuarantorUserId",
                schema: "leasing",
                table: "Leases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredSignaturesCount",
                schema: "leasing",
                table: "Leases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LeaseSignatures",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false),
                    Signed = table.Column<bool>(type: "boolean", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignatureRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SignedFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SignedFileHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SignatureCertSubject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SignatureVerified = table.Column<bool>(type: "boolean", nullable: false),
                    SigningIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    SigningUserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChallengeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    VerificationError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaseSignatures_Leases_LeaseId",
                        column: x => x.LeaseId,
                        principalSchema: "leasing",
                        principalTable: "Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeaseTermAcceptances",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedDocumentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseTermAcceptances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaseTermAcceptances_Leases_LeaseId",
                        column: x => x.LeaseId,
                        principalSchema: "leasing",
                        principalTable: "Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Leases_CoTenantId",
                schema: "leasing",
                table: "Leases",
                column: "CoTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Leases_GuarantorUserId",
                schema: "leasing",
                table: "Leases",
                column: "GuarantorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseSignatures_LeaseId_SequenceOrder",
                schema: "leasing",
                table: "LeaseSignatures",
                columns: new[] { "LeaseId", "SequenceOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaseSignatures_LeaseId_SignedFileHash",
                schema: "leasing",
                table: "LeaseSignatures",
                columns: new[] { "LeaseId", "SignedFileHash" },
                unique: true,
                filter: "\"SignedFileHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseSignatures_LeaseId_UserId_Role",
                schema: "leasing",
                table: "LeaseSignatures",
                columns: new[] { "LeaseId", "UserId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaseSignatures_SignatureRef",
                schema: "leasing",
                table: "LeaseSignatures",
                column: "SignatureRef",
                unique: true,
                filter: "\"SignatureRef\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeaseTermAcceptances_LeaseId_UserId",
                schema: "leasing",
                table: "LeaseTermAcceptances",
                columns: new[] { "LeaseId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaseSignatures",
                schema: "leasing");

            migrationBuilder.DropTable(
                name: "LeaseTermAcceptances",
                schema: "leasing");

            migrationBuilder.DropIndex(
                name: "IX_Leases_CoTenantId",
                schema: "leasing",
                table: "Leases");

            migrationBuilder.DropIndex(
                name: "IX_Leases_GuarantorUserId",
                schema: "leasing",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "CoTenantId",
                schema: "leasing",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "GuarantorRecordId",
                schema: "leasing",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "GuarantorUserId",
                schema: "leasing",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "RequiredSignaturesCount",
                schema: "leasing",
                table: "Leases");
        }
    }
}

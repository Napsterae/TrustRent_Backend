using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class MoveLeasesToLeasingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaseHistories",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Leases",
                schema: "catalog");

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseId",
                schema: "catalog",
                table: "Applications",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LeaseId",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.CreateTable(
                name: "Leases",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PropertyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvanceRentMonths = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    AllowsRenewal = table.Column<bool>(type: "boolean", nullable: false),
                    CondominiumFeesPaidBy = table.Column<string>(type: "text", nullable: false),
                    ContractFileHash = table.Column<string>(type: "text", nullable: true),
                    ContractFilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContractGeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContractSignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContractType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Deposit = table.Column<decimal>(type: "numeric", nullable: true),
                    DurationMonths = table.Column<int>(type: "integer", nullable: false),
                    ElectricityPaidBy = table.Column<string>(type: "text", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GasPaidBy = table.Column<string>(type: "text", nullable: false),
                    LandlordId = table.Column<Guid>(type: "uuid", nullable: false),
                    LandlordSignatureCertSubject = table.Column<string>(type: "text", nullable: true),
                    LandlordSignatureRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LandlordSignatureVerified = table.Column<bool>(type: "boolean", nullable: false),
                    LandlordSigned = table.Column<bool>(type: "boolean", nullable: false),
                    LandlordSignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LandlordSignedFileHash = table.Column<string>(type: "text", nullable: true),
                    LandlordSignedFilePath = table.Column<string>(type: "text", nullable: true),
                    LeaseRegime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MonthlyRent = table.Column<decimal>(type: "numeric", nullable: false),
                    RenewalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantSignatureCertSubject = table.Column<string>(type: "text", nullable: true),
                    TenantSignatureRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TenantSignatureVerified = table.Column<bool>(type: "boolean", nullable: false),
                    TenantSigned = table.Column<bool>(type: "boolean", nullable: false),
                    TenantSignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantSignedFilePath = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WaterPaidBy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leases_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "catalog",
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Leases_Properties_PropertyId",
                        column: x => x.PropertyId,
                        principalSchema: "catalog",
                        principalTable: "Properties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LeaseHistories",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventData = table.Column<string>(type: "text", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaseHistories_Leases_LeaseId",
                        column: x => x.LeaseId,
                        principalSchema: "catalog",
                        principalTable: "Leases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaseHistories_LeaseId",
                schema: "catalog",
                table: "LeaseHistories",
                column: "LeaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Leases_ApplicationId",
                schema: "catalog",
                table: "Leases",
                column: "ApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leases_PropertyId",
                schema: "catalog",
                table: "Leases",
                column: "PropertyId");
        }
    }
}

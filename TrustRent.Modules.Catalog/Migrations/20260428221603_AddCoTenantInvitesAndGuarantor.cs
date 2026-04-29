using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddCoTenantInvitesAndGuarantor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AcceptsGuarantor",
                schema: "catalog",
                table: "Properties",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GuarantorPolicyNote",
                schema: "catalog",
                table: "Properties",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoTenantEmployerName",
                schema: "catalog",
                table: "Applications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoTenantEmployerNif",
                schema: "catalog",
                table: "Applications",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoTenantEmploymentStartDate",
                schema: "catalog",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoTenantEmploymentType",
                schema: "catalog",
                table: "Applications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CoTenantIncomeRangeId",
                schema: "catalog",
                table: "Applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoTenantIncomeValidatedAt",
                schema: "catalog",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoTenantIncomeValidationMethod",
                schema: "catalog",
                table: "Applications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoTenantIncomeValidationRequestedAt",
                schema: "catalog",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CoTenantJoinedAt",
                schema: "catalog",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoTenantPayslipsProvidedCount",
                schema: "catalog",
                table: "Applications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CoTenantUserId",
                schema: "catalog",
                table: "Applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GuarantorId",
                schema: "catalog",
                table: "Applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuarantorRequestNote",
                schema: "catalog",
                table: "Applications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GuarantorRequestedAt",
                schema: "catalog",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GuarantorRequirementStatus",
                schema: "catalog",
                table: "Applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsCoTenantIncomeValidationRequested",
                schema: "catalog",
                table: "Applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsGuarantorRequired",
                schema: "catalog",
                table: "Applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ApplicationCoTenantInvites",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteeEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    InviteeUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeclineReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedFromIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationCoTenantInvites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationCoTenantInvites_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "catalog",
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Guarantors",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteStatus = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LandlordRequestNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeclineReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsIdentityVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IdentityVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IdentityMatchEvidenceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    IncomeRangeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IncomeValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmploymentType = table.Column<int>(type: "integer", nullable: true),
                    IncomeValidationMethod = table.Column<int>(type: "integer", nullable: true),
                    PayslipsProvidedCount = table.Column<int>(type: "integer", nullable: true),
                    EmployerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EmployerNif = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    EmploymentStartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guarantors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Guarantors_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "catalog",
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Guarantors_SalaryRanges_IncomeRangeId",
                        column: x => x.IncomeRangeId,
                        principalSchema: "catalog",
                        principalTable: "SalaryRanges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CoTenantIncomeRangeId",
                schema: "catalog",
                table: "Applications",
                column: "CoTenantIncomeRangeId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_CoTenantUserId",
                schema: "catalog",
                table: "Applications",
                column: "CoTenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_GuarantorId",
                schema: "catalog",
                table: "Applications",
                column: "GuarantorId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationCoTenantInvites_ApplicationId",
                schema: "catalog",
                table: "ApplicationCoTenantInvites",
                column: "ApplicationId",
                unique: true,
                filter: "\"Status\" IN (0,1)");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationCoTenantInvites_ApplicationId_InviteeEmail",
                schema: "catalog",
                table: "ApplicationCoTenantInvites",
                columns: new[] { "ApplicationId", "InviteeEmail" },
                unique: true,
                filter: "\"Status\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationCoTenantInvites_InviteeUserId_Status_ExpiresAt",
                schema: "catalog",
                table: "ApplicationCoTenantInvites",
                columns: new[] { "InviteeUserId", "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Guarantors_ApplicationId",
                schema: "catalog",
                table: "Guarantors",
                column: "ApplicationId",
                unique: true,
                filter: "\"InviteStatus\" IN (0,1)");

            migrationBuilder.CreateIndex(
                name: "IX_Guarantors_IncomeRangeId",
                schema: "catalog",
                table: "Guarantors",
                column: "IncomeRangeId");

            migrationBuilder.CreateIndex(
                name: "IX_Guarantors_UserId_InviteStatus",
                schema: "catalog",
                table: "Guarantors",
                columns: new[] { "UserId", "InviteStatus" });

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_SalaryRanges_CoTenantIncomeRangeId",
                schema: "catalog",
                table: "Applications",
                column: "CoTenantIncomeRangeId",
                principalSchema: "catalog",
                principalTable: "SalaryRanges",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_SalaryRanges_CoTenantIncomeRangeId",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropTable(
                name: "ApplicationCoTenantInvites",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "Guarantors",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_Applications_CoTenantIncomeRangeId",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_CoTenantUserId",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropIndex(
                name: "IX_Applications_GuarantorId",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "AcceptsGuarantor",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "GuarantorPolicyNote",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "CoTenantEmployerName",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CoTenantEmployerNif",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CoTenantEmploymentStartDate",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CoTenantEmploymentType",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CoTenantIncomeRangeId",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CoTenantIncomeValidatedAt",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CoTenantIncomeValidationMethod",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CoTenantIncomeValidationRequestedAt",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CoTenantJoinedAt",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CoTenantPayslipsProvidedCount",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "CoTenantUserId",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "GuarantorId",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "GuarantorRequestNote",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "GuarantorRequestedAt",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "GuarantorRequirementStatus",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "IsCoTenantIncomeValidationRequested",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "IsGuarantorRequired",
                schema: "catalog",
                table: "Applications");
        }
    }
}

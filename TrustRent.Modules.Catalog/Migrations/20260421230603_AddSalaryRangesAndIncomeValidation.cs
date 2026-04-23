using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddSalaryRangesAndIncomeValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IncomeRangeId",
                schema: "catalog",
                table: "Applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IncomeValidatedAt",
                schema: "catalog",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IncomeValidationRequestedAt",
                schema: "catalog",
                table: "Applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsIncomeValidationRequested",
                schema: "catalog",
                table: "Applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SalaryRanges",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MinAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    MaxAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryRanges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_IncomeRangeId",
                schema: "catalog",
                table: "Applications",
                column: "IncomeRangeId");

            migrationBuilder.CreateIndex(
                name: "IX_SalaryRanges_Code",
                schema: "catalog",
                table: "SalaryRanges",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_SalaryRanges_IncomeRangeId",
                schema: "catalog",
                table: "Applications",
                column: "IncomeRangeId",
                principalSchema: "catalog",
                principalTable: "SalaryRanges",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_SalaryRanges_IncomeRangeId",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropTable(
                name: "SalaryRanges",
                schema: "catalog");

            migrationBuilder.DropIndex(
                name: "IX_Applications_IncomeRangeId",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "IncomeRangeId",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "IncomeValidatedAt",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "IncomeValidationRequestedAt",
                schema: "catalog",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "IsIncomeValidationRequested",
                schema: "catalog",
                table: "Applications");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class AddRentIncreaseRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RentIncreaseRequests",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentRent = table.Column<decimal>(type: "numeric", nullable: false),
                    ProposedRent = table.Column<decimal>(type: "numeric", nullable: false),
                    IncreasePercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    CoefficientApplied = table.Column<decimal>(type: "numeric", nullable: false),
                    CoefficientYear = table.Column<int>(type: "integer", nullable: false),
                    AccumulatedCoefficients = table.Column<bool>(type: "boolean", nullable: false),
                    AccumulatedDetails = table.Column<string>(type: "text", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContestationDeadline = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Contested = table.Column<bool>(type: "boolean", nullable: false),
                    ContestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ContestationReason = table.Column<string>(type: "text", nullable: true),
                    ContestationResolution = table.Column<string>(type: "text", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Applied = table.Column<bool>(type: "boolean", nullable: false),
                    RequesterIpAddress = table.Column<string>(type: "text", nullable: true),
                    RequesterUserAgent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RentIncreaseRequests", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RentIncreaseRequests",
                schema: "leasing");
        }
    }
}

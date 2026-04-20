using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaseTerminationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaseTerminationRequests",
                schema: "leasing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uuid", nullable: false),
                    TerminationType = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequiredNoticeDays = table.Column<int>(type: "integer", nullable: false),
                    EarliestTerminationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProposedTerminationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OneThirdDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HasPassedOneThird = table.Column<bool>(type: "boolean", nullable: false),
                    IndemnificationAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    IndemnificationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IndemnificationReason = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedByNote = table.Column<string>(type: "text", nullable: true),
                    DeclaresNoAlternativeHousing = table.Column<bool>(type: "boolean", nullable: true),
                    BeneficiaryRelation = table.Column<string>(type: "text", nullable: true),
                    BeneficiaryName = table.Column<string>(type: "text", nullable: true),
                    RequesterIpAddress = table.Column<string>(type: "text", nullable: true),
                    RequesterUserAgent = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaseTerminationRequests", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaseTerminationRequests",
                schema: "leasing");
        }
    }
}

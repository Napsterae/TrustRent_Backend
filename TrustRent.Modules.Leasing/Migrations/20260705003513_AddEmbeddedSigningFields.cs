using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Leasing.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddedSigningFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalSignerId",
                schema: "leasing",
                table: "LeaseSignatures",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSigningRequestId",
                schema: "leasing",
                table: "Leases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SignatureProvider",
                schema: "leasing",
                table: "Leases",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalSignerId",
                schema: "leasing",
                table: "LeaseSignatures");

            migrationBuilder.DropColumn(
                name: "ExternalSigningRequestId",
                schema: "leasing",
                table: "Leases");

            migrationBuilder.DropColumn(
                name: "SignatureProvider",
                schema: "leasing",
                table: "Leases");
        }
    }
}

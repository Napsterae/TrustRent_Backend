using Microsoft.EntityFrameworkCore.Migrations;

namespace TrustRent.Modules.Leasing.Migrations;

public partial class AddSplitAndRefund : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "TenantSharePercentage",
            schema: "leasing",
            table: "Leases",
            type: "numeric(5,2)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "TenantSharePercentage",
            schema: "leasing",
            table: "Leases");
    }
}

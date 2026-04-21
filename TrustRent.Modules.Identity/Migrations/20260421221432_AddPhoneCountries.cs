using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneCountries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhoneCountries",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsoCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DialCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    MobilePattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Example = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FlagEmoji = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoneCountries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhoneCountries_IsoCode",
                schema: "identity",
                table: "PhoneCountries",
                column: "IsoCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhoneCountries",
                schema: "identity");
        }
    }
}

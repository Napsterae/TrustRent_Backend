using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestGuarantorTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: the guest guarantor token columns (GuestEmail, GuestAccessToken,
            // GuestName, GuestPhoneNumber, GuestTokenIssuedAt, GuestTokenLastUsedAt,
            // CreatedFromIp, UserId nullable, NOT NULL constraints and indexes) were
            // already delivered by the applied migration AddGuestGuarantorFields.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustRent.Modules.Identity.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Application code now always normalizes emails (trim + remove diacritics + lowercase)
    /// via <c>EmailHelper.NormalizeEmail</c> before persisting. The existing unique index
    /// on <c>"Email"</c> is therefore sufficient — no functional index is required.
    ///
    /// This migration only backfills any pre-existing rows whose email was stored before
    /// normalization was introduced. It is safe to re-run and a no-op on a fresh database.
    /// </remarks>
    public partial class AddCaseInsensitiveEmailNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Lightweight diacritic-stripping helper, scoped to the identity schema, used only
            // for the one-shot UPDATE below. No external extension required (works without
            // the unaccent extension on managed Postgres without superuser).
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION identity.tr_remove_diacritics(input text) RETURNS text AS $$
                DECLARE
                    result text := input;
                BEGIN
                    IF result IS NULL THEN RETURN NULL; END IF;
                    result := translate(result,
                        'áàâãäåÁÀÂÃÄÅéèêëÉÈÊËíìîïÍÌÎÏóòôõöÓÒÔÕÖúùûüÚÙÛÜçÇñÑýÿÝ',
                        'aaaaaaAAAAAAeeeeEEEEiiiiIIIIoooooOOOOOuuuuUUUUcCnNyyY');
                    RETURN result;
                END;
                $$ LANGUAGE plpgsql IMMUTABLE;
            ");

            // Backfill: normalize pre-existing rows. Skip rows that would create duplicates
            // (those must be reconciled manually). Quoted PascalCase identifiers are required
            // because EF created the column as "Email", not email.
            migrationBuilder.Sql(@"
                UPDATE identity.""Users"" AS u
                SET ""Email"" = lower(identity.tr_remove_diacritics(u.""Email""))
                WHERE u.""Email"" IS NOT NULL
                  AND u.""Email"" <> lower(identity.tr_remove_diacritics(u.""Email""))
                  AND NOT EXISTS (
                      SELECT 1 FROM identity.""Users"" AS o
                      WHERE o.""Id"" <> u.""Id""
                        AND o.""Email"" = lower(identity.tr_remove_diacritics(u.""Email""))
                  );
            ");

            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS identity.tr_remove_diacritics(text);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data backfill is not reversible (original casing/diacritics are lost).
        }
    }
}

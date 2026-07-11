using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class AddFullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Extensions (also declared via HasPostgresExtension in DbContext, but ensure they exist)
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");

            // Custom Portuguese text search config with accent stripping.
            // Falls back gracefully: if this fails (e.g., extension not available),
            // the computed column uses COALESCE to default to plain 'portuguese'.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_ts_config WHERE cfgname = 'pt_unaccent') THEN
                        CREATE TEXT SEARCH CONFIGURATION pt_unaccent (COPY = portuguese);
                        ALTER TEXT SEARCH CONFIGURATION pt_unaccent
                            ALTER MAPPING FOR hword, hword_part, word
                            WITH unaccent, portuguese_stem;
                    END IF;
                EXCEPTION WHEN OTHERS THEN
                    -- If unaccent or portuguese config is unavailable, skip silently.
                    -- The computed column will fall back to plain 'portuguese' via COALESCE.
                END $$;
            ");

            // Set the default search config at database level so the computed column uses pt_unaccent.
            // Uses SET LOCAL-safe approach: if pt_unaccent exists, use it; otherwise default to portuguese.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_ts_config WHERE cfgname = 'pt_unaccent') THEN
                        PERFORM set_config('app.search_config', 'pt_unaccent', false);
                    END IF;
                END $$;
            ");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                schema: "catalog",
                table: "Properties",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector(COALESCE(current_setting('app.search_config', true), 'portuguese'), coalesce(\"Title\", '')), 'A') || ' ' ||\r\n                         setweight(to_tsvector(COALESCE(current_setting('app.search_config', true), 'portuguese'), coalesce(\"Description\", '')), 'B') || ' ' ||\r\n                         setweight(to_tsvector(COALESCE(current_setting('app.search_config', true), 'portuguese'), coalesce(\"Municipality\", '') || ' ' || coalesce(\"District\", '') || ' ' || coalesce(\"Parish\", '')), 'C')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Properties_SearchVector",
                schema: "catalog",
                table: "Properties",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            // Trigram GIN index on Title for fuzzy/typo-tolerant search fallback
            migrationBuilder.Sql(
                @"CREATE INDEX IF NOT EXISTS ""IX_Properties_Title_Trgm""
                  ON ""catalog"".""Properties"" USING GIN (""Title"" gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop trigram index
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"catalog\".\"IX_Properties_Title_Trgm\";");

            migrationBuilder.DropIndex(
                name: "IX_Properties_SearchVector",
                schema: "catalog",
                table: "Properties");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                schema: "catalog",
                table: "Properties");

            // Drop custom text search config
            migrationBuilder.Sql("DROP TEXT SEARCH CONFIGURATION IF EXISTS pt_unaccent;");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");
        }
    }
}

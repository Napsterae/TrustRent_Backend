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
            // Extensions
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                schema: "catalog",
                table: "Properties",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector('portuguese', coalesce(\"Title\", '')), 'A') || ' ' ||\r\n                         setweight(to_tsvector('portuguese', coalesce(\"Description\", '')), 'B') || ' ' ||\r\n                         setweight(to_tsvector('portuguese', coalesce(\"Municipality\", '') || ' ' || coalesce(\"District\", '') || ' ' || coalesce(\"Parish\", '')), 'C')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Properties_SearchVector",
                schema: "catalog",
                table: "Properties",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            // Trigram GIN index on Title for fuzzy search
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

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");
        }
    }
}

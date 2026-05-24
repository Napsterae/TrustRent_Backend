using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TrustRent.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class SyncPropertyFeatureAmenities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "catalog",
                table: "Amenities",
                columns: new[] { "Id", "Category", "IconName", "Name" },
                values: new object[,]
                {
                    { new Guid("a0000000-0000-0000-0000-000000000013"), "Conforto", "ArrowUpCircle", "Elevador" },
                    { new Guid("a0000000-0000-0000-0000-000000000014"), "Extra", "Car", "Garagem / Parqueamento" },
                    { new Guid("a0000000-0000-0000-0000-000000000015"), "Básico", "BedDouble", "Mobilado / Equipado" }
                });

                        migrationBuilder.Sql(
                                """
                                INSERT INTO catalog."PropertyAmenities" ("PropertyId", "AmenityId")
                                SELECT p."Id", 'a0000000-0000-0000-0000-000000000005'
                                FROM catalog."Properties" p
                                LEFT JOIN catalog."PropertyAmenities" pa
                                        ON pa."PropertyId" = p."Id"
                                     AND pa."AmenityId" = 'a0000000-0000-0000-0000-000000000005'
                                WHERE p."HasAirConditioning" = TRUE
                                    AND pa."PropertyId" IS NULL;

                                INSERT INTO catalog."PropertyAmenities" ("PropertyId", "AmenityId")
                                SELECT p."Id", 'a0000000-0000-0000-0000-000000000010'
                                FROM catalog."Properties" p
                                LEFT JOIN catalog."PropertyAmenities" pa
                                        ON pa."PropertyId" = p."Id"
                                     AND pa."AmenityId" = 'a0000000-0000-0000-0000-000000000010'
                                WHERE p."AllowsPets" = TRUE
                                    AND pa."PropertyId" IS NULL;

                                INSERT INTO catalog."PropertyAmenities" ("PropertyId", "AmenityId")
                                SELECT p."Id", 'a0000000-0000-0000-0000-000000000013'
                                FROM catalog."Properties" p
                                LEFT JOIN catalog."PropertyAmenities" pa
                                        ON pa."PropertyId" = p."Id"
                                     AND pa."AmenityId" = 'a0000000-0000-0000-0000-000000000013'
                                WHERE p."HasElevator" = TRUE
                                    AND pa."PropertyId" IS NULL;

                                INSERT INTO catalog."PropertyAmenities" ("PropertyId", "AmenityId")
                                SELECT p."Id", 'a0000000-0000-0000-0000-000000000014'
                                FROM catalog."Properties" p
                                LEFT JOIN catalog."PropertyAmenities" pa
                                        ON pa."PropertyId" = p."Id"
                                     AND pa."AmenityId" = 'a0000000-0000-0000-0000-000000000014'
                                WHERE p."HasGarage" = TRUE
                                    AND pa."PropertyId" IS NULL;

                                INSERT INTO catalog."PropertyAmenities" ("PropertyId", "AmenityId")
                                SELECT p."Id", 'a0000000-0000-0000-0000-000000000015'
                                FROM catalog."Properties" p
                                LEFT JOIN catalog."PropertyAmenities" pa
                                        ON pa."PropertyId" = p."Id"
                                     AND pa."AmenityId" = 'a0000000-0000-0000-0000-000000000015'
                                WHERE p."IsFurnished" = TRUE
                                    AND pa."PropertyId" IS NULL;

                                UPDATE catalog."Properties" p
                                SET "HasAirConditioning" = TRUE
                                WHERE EXISTS (
                                        SELECT 1
                                        FROM catalog."PropertyAmenities" pa
                                        WHERE pa."PropertyId" = p."Id"
                                            AND pa."AmenityId" = 'a0000000-0000-0000-0000-000000000005'
                                );

                                UPDATE catalog."Properties" p
                                SET "AllowsPets" = TRUE
                                WHERE EXISTS (
                                        SELECT 1
                                        FROM catalog."PropertyAmenities" pa
                                        WHERE pa."PropertyId" = p."Id"
                                            AND pa."AmenityId" = 'a0000000-0000-0000-0000-000000000010'
                                );

                                UPDATE catalog."Properties" p
                                SET "HasElevator" = TRUE
                                WHERE EXISTS (
                                        SELECT 1
                                        FROM catalog."PropertyAmenities" pa
                                        WHERE pa."PropertyId" = p."Id"
                                            AND pa."AmenityId" = 'a0000000-0000-0000-0000-000000000013'
                                );

                                UPDATE catalog."Properties" p
                                SET "HasGarage" = TRUE
                                WHERE EXISTS (
                                        SELECT 1
                                        FROM catalog."PropertyAmenities" pa
                                        WHERE pa."PropertyId" = p."Id"
                                            AND pa."AmenityId" = 'a0000000-0000-0000-0000-000000000014'
                                );

                                UPDATE catalog."Properties" p
                                SET "IsFurnished" = TRUE
                                WHERE EXISTS (
                                        SELECT 1
                                        FROM catalog."PropertyAmenities" pa
                                        WHERE pa."PropertyId" = p."Id"
                                            AND pa."AmenityId" = 'a0000000-0000-0000-0000-000000000015'
                                );

                                UPDATE catalog."Properties"
                                SET "FurnishedDescription" = NULL
                                WHERE "IsFurnished" = FALSE;
                                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM catalog."PropertyAmenities"
                WHERE "AmenityId" IN (
                    'a0000000-0000-0000-0000-000000000013',
                    'a0000000-0000-0000-0000-000000000014',
                    'a0000000-0000-0000-0000-000000000015'
                );
                """);

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                schema: "catalog",
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000015"));
        }
    }
}

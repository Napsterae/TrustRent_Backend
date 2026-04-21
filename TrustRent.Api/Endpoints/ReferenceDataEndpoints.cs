using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Database;

namespace TrustRent.Api.Endpoints;

public static class ReferenceDataEndpoints
{
    // Cache no browser durante 1h; ETag faz revalidação rápida quando expira.
    private const int CacheSeconds = 3600;

    private static void ApplyCacheHeaders(HttpContext ctx)
    {
        ctx.Response.Headers[HeaderNames.CacheControl] = $"public, max-age={CacheSeconds}";
        ctx.Response.Headers[HeaderNames.Vary] = "Accept-Encoding";
    }

    public static void MapReferenceDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reference");

        // Lista leve de distritos (sem concelhos/freguesias). Carregada ao abrir
        // qualquer página com filtros ou cascading select.
        group.MapGet("/locations", async (CatalogDbContext db, HttpContext ctx) =>
        {
            ApplyCacheHeaders(ctx);

            var districts = await db.Districts
                .Where(d => d.IsActive)
                .OrderBy(d => d.DisplayOrder).ThenBy(d => d.Name)
                .AsNoTracking()
                .Select(d => new { id = d.Id, code = d.Code, district = d.Name })
                .ToListAsync();

            return Results.Ok(districts);
        });

        // Detalhes de UM distrito: concelhos + freguesias. Pedido lazy quando
        // o utilizador expande o distrito na sidebar ou escolhe-o no select.
        group.MapGet("/locations/{districtCode}", async (string districtCode, CatalogDbContext db, HttpContext ctx) =>
        {
            var district = await db.Districts
                .Where(d => d.IsActive && d.Code == districtCode)
                .Include(d => d.Municipalities.Where(m => m.IsActive))
                    .ThenInclude(m => m.Parishes.Where(p => p.IsActive))
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (district == null) return Results.NotFound();

            ApplyCacheHeaders(ctx);

            var payload = new
            {
                id = district.Id,
                code = district.Code,
                district = district.Name,
                municipalities = district.Municipalities
                    .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name)
                    .Select(m => new
                    {
                        id = m.Id,
                        code = m.Code,
                        name = m.Name,
                        parishes = m.Parishes
                            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
                            .Select(p => p.Name)
                            .ToList()
                    })
                    .ToList()
            };

            return Results.Ok(payload);
        });

        // Tipos de imóvel
        group.MapGet("/property-types", async (CatalogDbContext db, HttpContext ctx) =>
        {
            ApplyCacheHeaders(ctx);
            var items = await db.PropertyTypes
                .Where(t => t.IsActive)
                .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name)
                .AsNoTracking()
                .Select(t => new { t.Id, t.Code, t.Name })
                .ToListAsync();
            return Results.Ok(items);
        });

        // Tipologias
        group.MapGet("/typologies", async (CatalogDbContext db, HttpContext ctx) =>
        {
            ApplyCacheHeaders(ctx);
            var items = await db.Typologies
                .Where(t => t.IsActive)
                .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name)
                .AsNoTracking()
                .Select(t => new { t.Id, t.Code, t.Name, t.Bedrooms })
                .ToListAsync();
            return Results.Ok(items);
        });

        // Países de telefone
        group.MapGet("/phone-countries", async (IdentityDbContext db, HttpContext ctx) =>
        {
            ApplyCacheHeaders(ctx);
            var items = await db.PhoneCountries
                .Where(c => c.IsActive)
                .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
                .AsNoTracking()
                .Select(c => new
                {
                    code = c.IsoCode,
                    name = c.Name,
                    dialCode = c.DialCode,
                    flag = c.FlagEmoji,
                    mobilePattern = c.MobilePattern,
                    example = c.Example,
                    isDefault = c.IsDefault
                })
                .ToListAsync();
            return Results.Ok(items);
        });
    }
}

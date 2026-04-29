using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TrustRent.Modules.Admin.Authorization;
using TrustRent.Modules.Admin.Contracts;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Catalog.Models.ReferenceData;

namespace TrustRent.Modules.Admin.Endpoints;

internal static class AdminAuditExtensions
{
    public static Task WriteAsync(this IAuditLogService audit, Guid adminUserId, string action,
        string entityType, string? entityId, string? beforeJson, string? afterJson, string? reason, HttpContext ctx)
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString();
        var ua = ctx.Request.Headers.UserAgent.ToString();
        var corr = ctx.TraceIdentifier;
        return audit.WriteAsync(adminUserId, action, entityType, entityId, beforeJson, afterJson, reason, ip, ua, corr);
    }
}

public static class AdminReferenceDataEndpoints
{
    private static Guid GetAdminId(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? ctx.User.FindFirst("sub")!.Value);

    public static void MapAdminReferenceDataEndpoints(this IEndpointRouteBuilder app)
    {
        // ======= PROPERTY TYPES =======
        var pt = app.MapGroup("/api/admin/reference/property-types");

        pt.MapGet("/", async (CatalogDbContext db) =>
        {
            var items = await db.PropertyTypes
                .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name)
                .AsNoTracking()
                .ToListAsync();
            return Results.Ok(items);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferenceRead));

        pt.MapPost("/", async ([FromBody] PropertyType payload, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            payload.Id = Guid.NewGuid();
            payload.CreatedAt = DateTime.UtcNow;
            payload.IsSystemDefault = false;
            db.PropertyTypes.Add(payload);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "reference.property-type.create", "PropertyType", payload.Id.ToString(), null, JsonSerializer.Serialize(payload), null, ctx);
            return Results.Created($"/api/admin/reference/property-types/{payload.Id}", payload);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferencePropertyOptionsEdit));

        pt.MapPut("/{id:guid}", async (Guid id, [FromBody] PropertyType payload, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var entity = await db.PropertyTypes.FindAsync(id);
            if (entity is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(entity);
            entity.Code = payload.Code;
            entity.Name = payload.Name;
            entity.DisplayOrder = payload.DisplayOrder;
            entity.IsActive = payload.IsActive;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "reference.property-type.update", "PropertyType", id.ToString(), before, JsonSerializer.Serialize(entity), null, ctx);
            return Results.Ok(entity);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferencePropertyOptionsEdit));

        pt.MapDelete("/{id:guid}", async (Guid id, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var entity = await db.PropertyTypes.FindAsync(id);
            if (entity is null) return Results.NotFound();
            if (entity.IsSystemDefault) return Results.BadRequest(new { error = "Itens de sistema não podem ser eliminados." });
            var inUse = await db.Properties.AnyAsync(p => p.PropertyType == entity.Code);
            if (inUse) return Results.BadRequest(new { error = "Tipo em uso por imóveis. Desactive em vez de eliminar." });
            db.PropertyTypes.Remove(entity);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "reference.property-type.delete", "PropertyType", id.ToString(), JsonSerializer.Serialize(entity), null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferencePropertyOptionsEdit));

        // ======= TYPOLOGIES =======
        var tp = app.MapGroup("/api/admin/reference/typologies");

        tp.MapGet("/", async (CatalogDbContext db) =>
        {
            var items = await db.Typologies
                .OrderBy(t => t.DisplayOrder).ThenBy(t => t.Bedrooms)
                .AsNoTracking().ToListAsync();
            return Results.Ok(items);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferenceRead));

        tp.MapPost("/", async ([FromBody] Typology payload, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            payload.Id = Guid.NewGuid();
            payload.CreatedAt = DateTime.UtcNow;
            payload.IsSystemDefault = false;
            db.Typologies.Add(payload);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "reference.typology.create", "Typology", payload.Id.ToString(), null, JsonSerializer.Serialize(payload), null, ctx);
            return Results.Created($"/api/admin/reference/typologies/{payload.Id}", payload);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferencePropertyOptionsEdit));

        tp.MapPut("/{id:guid}", async (Guid id, [FromBody] Typology payload, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var entity = await db.Typologies.FindAsync(id);
            if (entity is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(entity);
            entity.Code = payload.Code;
            entity.Name = payload.Name;
            entity.Bedrooms = payload.Bedrooms;
            entity.DisplayOrder = payload.DisplayOrder;
            entity.IsActive = payload.IsActive;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "reference.typology.update", "Typology", id.ToString(), before, JsonSerializer.Serialize(entity), null, ctx);
            return Results.Ok(entity);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferencePropertyOptionsEdit));

        tp.MapDelete("/{id:guid}", async (Guid id, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var entity = await db.Typologies.FindAsync(id);
            if (entity is null) return Results.NotFound();
            if (entity.IsSystemDefault) return Results.BadRequest(new { error = "Itens de sistema não podem ser eliminados." });
            var inUse = await db.Properties.AnyAsync(p => p.Typology == entity.Code);
            if (inUse) return Results.BadRequest(new { error = "Tipologia em uso. Desactive em vez de eliminar." });
            db.Typologies.Remove(entity);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "reference.typology.delete", "Typology", id.ToString(), JsonSerializer.Serialize(entity), null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferencePropertyOptionsEdit));

        // ======= SALARY RANGES =======
        var sr = app.MapGroup("/api/admin/reference/salary-ranges");

        sr.MapGet("/", async (CatalogDbContext db) =>
        {
            var items = await db.SalaryRanges.OrderBy(r => r.DisplayOrder).AsNoTracking().ToListAsync();
            return Results.Ok(items);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferenceRead));

        sr.MapPost("/", async ([FromBody] SalaryRange payload, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            payload.Id = Guid.NewGuid();
            payload.CreatedAt = DateTime.UtcNow;
            payload.IsSystemDefault = false;
            db.SalaryRanges.Add(payload);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "reference.salary-range.create", "SalaryRange", payload.Id.ToString(), null, JsonSerializer.Serialize(payload), null, ctx);
            return Results.Created($"/api/admin/reference/salary-ranges/{payload.Id}", payload);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferenceSalaryRangesEdit));

        sr.MapPut("/{id:guid}", async (Guid id, [FromBody] SalaryRange payload, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var entity = await db.SalaryRanges.FindAsync(id);
            if (entity is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(entity);
            entity.Code = payload.Code;
            entity.Label = payload.Label;
            entity.MinAmount = payload.MinAmount;
            entity.MaxAmount = payload.MaxAmount;
            entity.DisplayOrder = payload.DisplayOrder;
            entity.IsActive = payload.IsActive;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "reference.salary-range.update", "SalaryRange", id.ToString(), before, JsonSerializer.Serialize(entity), null, ctx);
            return Results.Ok(entity);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferenceSalaryRangesEdit));

        sr.MapDelete("/{id:guid}", async (Guid id, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var entity = await db.SalaryRanges.FindAsync(id);
            if (entity is null) return Results.NotFound();
            if (entity.IsSystemDefault) return Results.BadRequest(new { error = "Itens de sistema não podem ser eliminados." });
            db.SalaryRanges.Remove(entity);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "reference.salary-range.delete", "SalaryRange", id.ToString(), JsonSerializer.Serialize(entity), null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferenceSalaryRangesEdit));

        // ======= AMENITIES =======
        var am = app.MapGroup("/api/admin/reference/amenities");

        am.MapGet("/", async (CatalogDbContext db) =>
        {
            var items = await db.Amenities.OrderBy(a => a.Category).ThenBy(a => a.Name).AsNoTracking().ToListAsync();
            return Results.Ok(items);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferenceRead));

        am.MapPost("/", async ([FromBody] Amenity payload, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            payload.Id = Guid.NewGuid();
            db.Amenities.Add(payload);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "reference.amenity.create", "Amenity", payload.Id.ToString(), null, JsonSerializer.Serialize(payload), null, ctx);
            return Results.Created($"/api/admin/reference/amenities/{payload.Id}", payload);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferenceAmenitiesEdit));

        am.MapPut("/{id:guid}", async (Guid id, [FromBody] Amenity payload, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var entity = await db.Amenities.FindAsync(id);
            if (entity is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(entity);
            entity.Name = payload.Name;
            entity.IconName = payload.IconName;
            entity.Category = payload.Category;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "reference.amenity.update", "Amenity", id.ToString(), before, JsonSerializer.Serialize(entity), null, ctx);
            return Results.Ok(entity);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferenceAmenitiesEdit));

        am.MapDelete("/{id:guid}", async (Guid id, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var entity = await db.Amenities.FindAsync(id);
            if (entity is null) return Results.NotFound();
            var inUse = await db.PropertyAmenities.AnyAsync(p => p.AmenityId == id);
            if (inUse) return Results.BadRequest(new { error = "Comodidade em uso por imóveis." });
            db.Amenities.Remove(entity);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "reference.amenity.delete", "Amenity", id.ToString(), JsonSerializer.Serialize(entity), null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.ReferenceAmenitiesEdit));
    }
}

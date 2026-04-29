using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TrustRent.Modules.Admin.Authorization;
using TrustRent.Modules.Admin.Contracts;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Admin.Models;

namespace TrustRent.Modules.Admin.Endpoints;

public static class AdminSettingsEndpoints
{
    private static Guid GetAdminId(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? ctx.User.FindFirst("sub")!.Value);

    public record UpsertSettingRequest(string Key, string? Value, string? Category, string? Description, string? ValueType);
    public record UpsertFeatureFlagRequest(string Key, bool Enabled, int RolloutPercent, string? Description);

    public static void MapAdminSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var s = app.MapGroup("/api/admin/settings");

        s.MapGet("/", async (AdminDbContext db) =>
        {
            var items = await db.PlatformSettings.OrderBy(x => x.Category).ThenBy(x => x.Key).AsNoTracking().ToListAsync();
            return Results.Ok(items);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.SettingsRead));

        s.MapPut("/", async ([FromBody] UpsertSettingRequest req, AdminDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Key)) return Results.BadRequest(new { error = "Key obrigatória." });
            var existing = await db.PlatformSettings.FirstOrDefaultAsync(x => x.Key == req.Key);
            string? before = existing is null ? null : JsonSerializer.Serialize(existing);
            if (existing is null)
            {
                existing = new PlatformSetting { Key = req.Key };
                db.PlatformSettings.Add(existing);
            }
            existing.Value = req.Value;
            existing.Category = req.Category;
            existing.Description = req.Description;
            existing.ValueType = string.IsNullOrWhiteSpace(req.ValueType) ? "string" : req.ValueType!;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedByAdminId = GetAdminId(ctx);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "settings.upsert", "PlatformSetting", existing.Key, before, JsonSerializer.Serialize(existing), null, ctx);
            return Results.Ok(existing);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.SettingsEdit));

        s.MapDelete("/{key}", async (string key, AdminDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var existing = await db.PlatformSettings.FirstOrDefaultAsync(x => x.Key == key);
            if (existing is null) return Results.NotFound();
            db.PlatformSettings.Remove(existing);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "settings.delete", "PlatformSetting", key, JsonSerializer.Serialize(existing), null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.SettingsEdit));

        var f = app.MapGroup("/api/admin/feature-flags");

        f.MapGet("/", async (AdminDbContext db) =>
        {
            var items = await db.FeatureFlags.OrderBy(x => x.Key).AsNoTracking().ToListAsync();
            return Results.Ok(items);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.SettingsRead));

        f.MapPut("/", async ([FromBody] UpsertFeatureFlagRequest req, AdminDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(req.Key)) return Results.BadRequest(new { error = "Key obrigatória." });
            if (req.RolloutPercent < 0 || req.RolloutPercent > 100) return Results.BadRequest(new { error = "RolloutPercent entre 0 e 100." });
            var existing = await db.FeatureFlags.FirstOrDefaultAsync(x => x.Key == req.Key);
            string? before = existing is null ? null : JsonSerializer.Serialize(existing);
            if (existing is null)
            {
                existing = new FeatureFlag { Key = req.Key };
                db.FeatureFlags.Add(existing);
            }
            existing.Enabled = req.Enabled;
            existing.RolloutPercent = req.RolloutPercent;
            existing.Description = req.Description;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedByAdminId = GetAdminId(ctx);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "feature-flag.upsert", "FeatureFlag", existing.Key, before, JsonSerializer.Serialize(existing), null, ctx);
            return Results.Ok(existing);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.SettingsFeatureFlags));

        f.MapDelete("/{key}", async (string key, AdminDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var existing = await db.FeatureFlags.FirstOrDefaultAsync(x => x.Key == key);
            if (existing is null) return Results.NotFound();
            db.FeatureFlags.Remove(existing);
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "feature-flag.delete", "FeatureFlag", key, JsonSerializer.Serialize(existing), null, null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.SettingsFeatureFlags));
    }
}

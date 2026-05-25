using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TrustRent.Modules.Admin;
using TrustRent.Modules.Admin.Authorization;
using TrustRent.Modules.Admin.Contracts;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Database;

namespace TrustRent.Modules.Admin.Endpoints;

public static class AdminPropertiesEndpoints
{
    private static Guid GetAdminId(HttpContext ctx) =>
        Guid.Parse(ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? ctx.User.FindFirst("sub")!.Value);

    private static string NormalizeDigits(string? value) => new((value ?? string.Empty).Where(char.IsDigit).ToArray());

    public record ModerateRequest(string Status, string? Reason); // approved|rejected|pending
    public record BlockRequest(string Reason);
    public record FeatureRequest(bool Featured);

    public static void MapAdminPropertiesEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin/properties");

        g.MapGet("/", async ([FromQuery] string? q, [FromQuery] string? ownerQ, [FromQuery] string? status, [FromQuery] bool? blocked,
                              [FromQuery] int page, [FromQuery] int pageSize, CatalogDbContext db, IdentityDbContext identityDb) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 25 : (pageSize > 200 ? 200 : pageSize);
            var query = db.Properties.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var like = $"%{q.Trim().ToLower()}%";
                query = query.Where(p => EF.Functions.ILike(p.Title, like) || EF.Functions.ILike(p.Municipality, like));
            }
            if (!string.IsNullOrWhiteSpace(ownerQ))
            {
                var ownerLike = $"%{ownerQ.Trim().ToLower()}%";
                var ownerDigits = NormalizeDigits(ownerQ);
                var matchingLandlordIds = await identityDb.Users
                    .AsNoTracking()
                    .Where(u => EF.Functions.ILike(u.Name, ownerLike)
                        || (!string.IsNullOrWhiteSpace(ownerDigits) && (u.Nif == ownerDigits || u.CitizenCardNumber == ownerDigits)))
                    .Select(u => u.Id)
                    .ToListAsync();

                query = query.Where(p => matchingLandlordIds.Contains(p.LandlordId));
            }
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(p => p.ModerationStatus == status);
            if (blocked.HasValue) query = query.Where(p => p.IsBlocked == blocked.Value);
            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(p => new
                {
                    p.Id, p.Title, p.LandlordId, p.Price, p.Municipality, p.District,
                    p.ModerationStatus, p.IsBlocked, p.IsPublic, p.IsFeatured, p.CreatedAt
                })
                .ToListAsync();

            var landlordIds = items.Select(p => p.LandlordId).Distinct().ToList();
            var landlords = await identityDb.Users
                .AsNoTracking()
                .Where(u => landlordIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name, u.Nif })
                .ToDictionaryAsync(u => u.Id);

            return Results.Ok(new
            {
                items = items.Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.LandlordId,
                    p.Price,
                    p.Municipality,
                    p.District,
                    p.ModerationStatus,
                    p.IsBlocked,
                    p.IsPublic,
                    p.IsFeatured,
                    p.CreatedAt,
                    Landlord = landlords.TryGetValue(p.LandlordId, out var landlord)
                        ? new { landlord.Id, landlord.Name, landlord.Nif }
                        : null,
                }),
                page,
                pageSize,
                totalCount = total,
            });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PropertiesRead));

        g.MapGet("/{id:guid}", async (Guid id, CatalogDbContext db, IdentityDbContext identityDb, AdminDbContext adminDb) =>
        {
            var p = await db.Properties
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.LandlordId,
                    x.TenantId,
                    x.Title,
                    x.Description,
                    x.Price,
                    x.PropertyType,
                    x.Typology,
                    x.Area,
                    x.Rooms,
                    x.Bathrooms,
                    x.Floor,
                    x.HasElevator,
                    x.HasAirConditioning,
                    x.HasGarage,
                    x.AllowsPets,
                    x.IsFurnished,
                    x.FurnishedDescription,
                    x.District,
                    x.Municipality,
                    x.Parish,
                    x.DoorNumber,
                    x.Street,
                    x.PostalCode,
                    x.Latitude,
                    x.Longitude,
                    x.IsPublic,
                    x.IsUnderMaintenance,
                    x.CreatedAt,
                    x.UpdatedAt,
                    x.ModerationStatus,
                    x.ModerationReason,
                    x.ModeratedAt,
                    x.ModeratedByAdminId,
                    x.IsBlocked,
                    x.BlockedAt,
                    x.BlockedByAdminId,
                    x.BlockReason,
                    x.IsFeatured,
                    x.MatrixArticle,
                    x.PropertyFraction,
                    x.EnergyClass,
                    x.EnergyCertificateNumber,
                    x.EnergyCertificateExpiryDate,
                    x.AtRegistrationNumber,
                    x.ParishConcelho,
                    x.PermanentCertNumber,
                    x.PermanentCertOffice,
                    x.UsageLicenseNumber,
                    x.UsageLicenseDate,
                    x.UsageLicenseIssuer,
                    x.Deposit,
                    x.AdvanceRentMonths,
                    x.CondominiumFeesPaidBy,
                    x.WaterPaidBy,
                    x.ElectricityPaidBy,
                    x.GasPaidBy,
                    x.HasOfficialContract,
                    x.AcceptsGuarantor,
                    x.GuarantorPolicyNote,
                    x.LeaseRegime,
                    x.AllowsRenewal,
                    x.NonPermanentReason,
                    AcceptedPeriodicities = x.AcceptedPeriodicities
                        .Select(periodicity => periodicity.DurationMonths)
                        .OrderBy(months => months)
                        .ToList(),
                    Images = x.Images
                        .OrderByDescending(image => image.IsMain)
                        .ThenBy(image => image.Id)
                        .Select(image => new
                        {
                            image.Id,
                            image.Url,
                            image.Category,
                            image.IsMain,
                        })
                        .ToList(),
                    Amenities = x.Amenities
                        .Select(a => new
                        {
                            a.PropertyId,
                            a.AmenityId,
                            Amenity = new
                            {
                                a.Amenity.Id,
                                a.Amenity.Name,
                                a.Amenity.IconName,
                                a.Amenity.Category,
                            },
                        })
                        .ToList(),
                })
                .FirstOrDefaultAsync();

            if (p is null)
            {
                return Results.NotFound(new { error = "Imóvel não encontrado." });
            }

            var landlord = await identityDb.Users
                .AsNoTracking()
                .Where(u => u.Id == p.LandlordId)
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Email,
                    u.Nif,
                    u.PhoneCountryCode,
                    u.PhoneNumber,
                    u.PhoneContactPlatform,
                    u.IsSuspended,
                })
                .FirstOrDefaultAsync();

            var tenant = p.TenantId.HasValue
                ? await identityDb.Users
                    .AsNoTracking()
                    .Where(u => u.Id == p.TenantId.Value)
                    .Select(u => new
                    {
                        u.Id,
                        u.Name,
                        u.Email,
                        u.Nif,
                        u.PhoneCountryCode,
                        u.PhoneNumber,
                        u.PhoneContactPlatform,
                        u.IsSuspended,
                    })
                    .FirstOrDefaultAsync()
                : null;

            var moderatedByAdmin = p.ModeratedByAdminId.HasValue
                ? await adminDb.AdminUsers
                    .AsNoTracking()
                    .Where(admin => admin.Id == p.ModeratedByAdminId.Value)
                    .Select(admin => new
                    {
                        admin.Id,
                        admin.Name,
                        admin.Email,
                        admin.IsActive,
                    })
                    .FirstOrDefaultAsync()
                : null;

            var blockedByAdmin = p.BlockedByAdminId.HasValue
                ? await adminDb.AdminUsers
                    .AsNoTracking()
                    .Where(admin => admin.Id == p.BlockedByAdminId.Value)
                    .Select(admin => new
                    {
                        admin.Id,
                        admin.Name,
                        admin.Email,
                        admin.IsActive,
                    })
                    .FirstOrDefaultAsync()
                : null;

            return Results.Ok(new
            {
                p.Id,
                p.LandlordId,
                p.TenantId,
                p.Title,
                p.Description,
                p.Price,
                p.PropertyType,
                p.Typology,
                p.Area,
                p.Rooms,
                p.Bathrooms,
                p.Floor,
                p.HasElevator,
                p.HasAirConditioning,
                p.HasGarage,
                p.AllowsPets,
                p.IsFurnished,
                p.FurnishedDescription,
                p.District,
                p.Municipality,
                p.Parish,
                p.DoorNumber,
                p.Street,
                p.PostalCode,
                p.Latitude,
                p.Longitude,
                p.IsPublic,
                p.IsUnderMaintenance,
                p.CreatedAt,
                p.UpdatedAt,
                p.ModerationStatus,
                p.ModerationReason,
                p.ModeratedAt,
                p.ModeratedByAdminId,
                p.IsBlocked,
                p.BlockedAt,
                p.BlockedByAdminId,
                p.BlockReason,
                p.IsFeatured,
                p.MatrixArticle,
                p.PropertyFraction,
                p.EnergyClass,
                p.EnergyCertificateNumber,
                p.EnergyCertificateExpiryDate,
                p.AtRegistrationNumber,
                p.ParishConcelho,
                p.PermanentCertNumber,
                p.PermanentCertOffice,
                p.UsageLicenseNumber,
                p.UsageLicenseDate,
                p.UsageLicenseIssuer,
                p.Deposit,
                p.AdvanceRentMonths,
                p.CondominiumFeesPaidBy,
                p.WaterPaidBy,
                p.ElectricityPaidBy,
                p.GasPaidBy,
                p.HasOfficialContract,
                p.AcceptsGuarantor,
                p.GuarantorPolicyNote,
                p.LeaseRegime,
                p.AllowsRenewal,
                p.NonPermanentReason,
                p.AcceptedPeriodicities,
                p.Images,
                p.Amenities,
                Landlord = landlord,
                Tenant = tenant,
                ModeratedByAdmin = moderatedByAdmin,
                BlockedByAdmin = blockedByAdmin,
            });
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PropertiesRead));

        g.MapPost("/{id:guid}/moderate", async (Guid id, [FromBody] ModerateRequest req, CatalogDbContext db, IAuditLogService audit, IPermissionService permissions, HttpContext ctx) =>
        {
            var p = await db.Properties.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            var status = (req.Status ?? "").ToLowerInvariant();
            if (status is not ("approved" or "rejected" or "pending"))
                return Results.BadRequest(new { error = "Status inválido (approved|rejected|pending)." });
            var perm = status == "approved" ? PermissionCodes.PropertiesApprove
                     : status == "rejected" ? PermissionCodes.PropertiesReject
                     : PermissionCodes.PropertiesEdit;
            var adminId = GetAdminId(ctx);
            if (!await permissions.HasPermissionAsync(adminId, perm)) return Results.Forbid();
            var before = JsonSerializer.Serialize(new { p.ModerationStatus, p.ModerationReason, p.IsPublic });
            p.ModerationStatus = status;
            p.ModerationReason = req.Reason;
            p.ModeratedAt = DateTime.UtcNow;
            p.ModeratedByAdminId = adminId;
            if (status == "approved") p.IsPublic = true;
            if (status == "rejected") p.IsPublic = false;
            await db.SaveChangesAsync();
            await audit.WriteAsync(adminId, $"property.moderate.{status}", "Property", id.ToString(), before, JsonSerializer.Serialize(new { p.ModerationStatus, p.ModerationReason, p.IsPublic }), req.Reason, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminModuleExtensions.AdminPolicy);

        g.MapPost("/{id:guid}/block", async (Guid id, [FromBody] BlockRequest req, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var p = await db.Properties.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            if (p.IsBlocked) return Results.BadRequest(new { error = "Já bloqueado." });
            var before = JsonSerializer.Serialize(new { p.IsBlocked, p.IsPublic });
            p.IsBlocked = true;
            p.BlockedAt = DateTime.UtcNow;
            p.BlockedByAdminId = GetAdminId(ctx);
            p.BlockReason = req.Reason;
            p.IsPublic = false;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "property.block", "Property", id.ToString(), before, JsonSerializer.Serialize(new { p.IsBlocked, p.IsPublic }), req.Reason, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PropertiesBlock));

        g.MapPost("/{id:guid}/unblock", async (Guid id, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var p = await db.Properties.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            if (!p.IsBlocked) return Results.BadRequest(new { error = "Não está bloqueado." });
            var before = JsonSerializer.Serialize(new { p.IsBlocked });
            p.IsBlocked = false;
            p.BlockedAt = null;
            p.BlockedByAdminId = null;
            p.BlockReason = null;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), "property.unblock", "Property", id.ToString(), before, JsonSerializer.Serialize(new { p.IsBlocked }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PropertiesBlock));

        g.MapPost("/{id:guid}/feature", async (Guid id, [FromBody] FeatureRequest req, CatalogDbContext db, IAuditLogService audit, HttpContext ctx) =>
        {
            var p = await db.Properties.FirstOrDefaultAsync(x => x.Id == id);
            if (p is null) return Results.NotFound();
            var before = JsonSerializer.Serialize(new { p.IsFeatured });
            p.IsFeatured = req.Featured;
            await db.SaveChangesAsync();
            await audit.WriteAsync(GetAdminId(ctx), req.Featured ? "property.feature" : "property.unfeature", "Property", id.ToString(), before, JsonSerializer.Serialize(new { p.IsFeatured }), null, ctx);
            return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.PropertiesEdit));
    }
}

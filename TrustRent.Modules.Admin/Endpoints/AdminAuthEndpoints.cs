using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Contracts.DTOs;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Admin.Services;

namespace TrustRent.Modules.Admin.Endpoints;

public static class AdminAuthEndpoints
{
    public static void MapAdminAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/auth")
            .RequireRateLimiting("admin-auth");

        group.MapPost("/login", async ([FromBody] AdminLoginRequest request, HttpContext ctx, IAdminAuthService auth, IConfiguration cfg) =>
        {
            try
            {
                var ip = ctx.Connection.RemoteIpAddress?.ToString();
                var ua = ctx.Request.Headers.UserAgent.ToString();
                var result = await auth.LoginAsync(request.Email, request.Password, request.MfaCode, ip, ua);

                AppendAdminCookie(ctx, result.Jwt, result.ExpiresAt, cfg);

                var dto = await BuildSelfDtoAsync(ctx.RequestServices, result.AdminUser.Id);
                return Results.Ok(new AdminLoginResponse(
                    AdminUser: dto!,
                    Permissions: result.Permissions.OrderBy(p => p).ToList(),
                    IsSuperAdmin: result.AdminUser.IsSuperAdmin,
                    PermissionsVersion: result.AdminUser.PermissionsVersion,
                    MfaRequired: false,
                    MustChangePassword: result.AdminUser.MustChangePassword,
                    MfaSetupRequired: result.AdminUser.IsSuperAdmin && !result.AdminUser.MfaEnabled,
                    CsrfToken: result.CsrfToken
                ));
            }
            catch (MfaRequiredException)
            {
                return Results.Json(new { error = "MFA obrigatório.", mfaRequired = true }, statusCode: 401);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: 401);
            }
        });

        group.MapPost("/logout", async (HttpContext ctx, IAdminAuthService auth) =>
        {
            var jti = ctx.User?.FindFirst("jti")?.Value;
            if (!string.IsNullOrEmpty(jti)) await auth.LogoutAsync(jti);
            ctx.Response.Cookies.Delete(AdminModuleExtensions.AdminCookieName, new CookieOptions
            {
                Path = "/",
                Secure = ctx.Request.IsHttps,
                SameSite = SameSiteMode.Strict,
                HttpOnly = true
            });
            return Results.Ok(new { message = "Sessão administrativa terminada." });
        }).RequireAuthorization(AdminModuleExtensions.AdminPolicy);

        group.MapGet("/me", async (HttpContext ctx, IAdminUserService svc, IPermissionService perms) =>
        {
            var id = GetAdminId(ctx);
            if (id is null) return Results.Unauthorized();
            var dto = await svc.GetAsync(id.Value);
            if (dto is null) return Results.Unauthorized();
            var permissions = await perms.GetEffectivePermissionsAsync(id.Value);
            return Results.Ok(new
            {
                adminUser = dto,
                permissions = permissions.OrderBy(p => p).ToList(),
                isSuperAdmin = dto.IsSuperAdmin,
                csrfToken = ctx.User?.FindFirst("csrf")?.Value
            });
        }).RequireAuthorization(AdminModuleExtensions.AdminPolicy);

        group.MapPost("/change-password", async ([FromBody] ChangePasswordRequest req, HttpContext ctx, IAdminAuthService auth) =>
        {
            var id = GetAdminId(ctx);
            if (id is null) return Results.Unauthorized();
            try
            {
                await auth.ChangePasswordAsync(id.Value, req.CurrentPassword, req.NewPassword);
                return Results.Ok(new { message = "Password alterada." });
            }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(AdminModuleExtensions.AdminPolicy);

        group.MapPost("/mfa/setup", async (HttpContext ctx, AdminDbContext db, IConfiguration cfg) =>
        {
            var id = GetAdminId(ctx);
            if (id is null) return Results.Unauthorized();
            var admin = await db.AdminUsers.FirstOrDefaultAsync(x => x.Id == id.Value && x.DeletedAt == null);
            if (admin is null) return Results.Unauthorized();
            if (admin.MfaEnabled) return Results.BadRequest(new { error = "MFA já está activo." });

            admin.MfaSecret = TotpHelper.GenerateSecret();
            await db.SaveChangesAsync();
            var issuer = cfg["AdminJwtSettings:Issuer"] ?? "TrustRent.Admin";
            return Results.Ok(new
            {
                secret = admin.MfaSecret,
                otpauthUri = TotpHelper.BuildOtpAuthUri(issuer, admin.Email, admin.MfaSecret)
            });
        }).RequireAuthorization(AdminModuleExtensions.AdminPolicy);

        group.MapPost("/mfa/enable", async ([FromBody] EnableMfaRequest req, HttpContext ctx, AdminDbContext db) =>
        {
            var id = GetAdminId(ctx);
            if (id is null) return Results.Unauthorized();
            var admin = await db.AdminUsers.FirstOrDefaultAsync(x => x.Id == id.Value && x.DeletedAt == null);
            if (admin is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(admin.MfaSecret)) return Results.BadRequest(new { error = "Inicie primeiro a configuração MFA." });
            if (!TotpHelper.VerifyCode(admin.MfaSecret, req.Code)) return Results.BadRequest(new { error = "Código MFA inválido." });

            admin.MfaEnabled = true;
            admin.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(new { message = "MFA activo." });
        }).RequireAuthorization(AdminModuleExtensions.AdminPolicy);

        group.MapGet("/sessions", async (HttpContext ctx, AdminDbContext db) =>
        {
            var id = GetAdminId(ctx);
            if (id is null) return Results.Unauthorized();
            var sessions = await db.Sessions.AsNoTracking()
                .Where(s => s.AdminUserId == id.Value)
                .OrderByDescending(s => s.CreatedAt)
                .Take(50)
                .Select(s => new { s.Id, s.CreatedAt, s.ExpiresAt, s.RevokedAt, s.RevokedReason, s.Ip, s.UserAgent })
                .ToListAsync();
            return Results.Ok(sessions);
        }).RequireAuthorization(AdminModuleExtensions.AdminPolicy);

        group.MapDelete("/sessions/{id:guid}", async (Guid id, HttpContext ctx, AdminDbContext db) =>
        {
            var adminId = GetAdminId(ctx);
            if (adminId is null) return Results.Unauthorized();
            var s = await db.Sessions.FirstOrDefaultAsync(x => x.Id == id && x.AdminUserId == adminId.Value);
            if (s is null) return Results.NotFound();
            if (s.RevokedAt is null) { s.RevokedAt = DateTime.UtcNow; s.RevokedReason = "self_revoke"; await db.SaveChangesAsync(); }
            return Results.NoContent();
        }).RequireAuthorization(AdminModuleExtensions.AdminPolicy);
    }

    public static Guid? GetAdminId(HttpContext ctx)
    {
        var sub = ctx.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? ctx.User?.FindFirst("sub")?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static async Task<AdminUserDto?> BuildSelfDtoAsync(IServiceProvider sp, Guid id)
    {
        var svc = sp.GetService(typeof(IAdminUserService)) as IAdminUserService;
        return svc is null ? null : await svc.GetAsync(id);
    }

    private static void AppendAdminCookie(HttpContext ctx, string token, DateTime expiresAt, IConfiguration cfg)
    {
        var domain = cfg["AdminCookieSettings:Domain"];
        ctx.Response.Cookies.Append(AdminModuleExtensions.AdminCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = expiresAt,
            IsEssential = true,
            Domain = string.IsNullOrWhiteSpace(domain) ? null : domain
        });
    }
}

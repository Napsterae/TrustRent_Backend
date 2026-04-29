using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TrustRent.Modules.Admin.Contracts;
using TrustRent.Modules.Admin.Contracts.DTOs;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Admin.Authorization;

namespace TrustRent.Modules.Admin.Endpoints;

public static class AdminUsersEndpoints
{
    public static void MapAdminUsersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin");

        // Permission catalog (read)
        group.MapGet("/permissions", async (IPermissionCatalogService svc) =>
            Results.Ok(await svc.GetCatalogAsync()))
            .RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsRead));

        // Roles
        group.MapGet("/roles", async (IRoleService svc) =>
            Results.Ok(await svc.ListAsync()))
            .RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsRead));

        group.MapPost("/roles", async ([FromBody] CreateOrUpdateRoleRequest req, HttpContext ctx, IRoleService svc) =>
        {
            var actor = AdminAuthEndpoints.GetAdminId(ctx)!.Value;
            try { return Results.Ok(await svc.CreateAsync(req, actor)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsManageRoles));

        group.MapPut("/roles/{id:guid}", async (Guid id, [FromBody] CreateOrUpdateRoleRequest req, HttpContext ctx, IRoleService svc) =>
        {
            var actor = AdminAuthEndpoints.GetAdminId(ctx)!.Value;
            try { return Results.Ok(await svc.UpdateAsync(id, req, actor)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsManageRoles));

        group.MapDelete("/roles/{id:guid}", async (Guid id, HttpContext ctx, IRoleService svc) =>
        {
            var actor = AdminAuthEndpoints.GetAdminId(ctx)!.Value;
            try { await svc.DeleteAsync(id, actor); return Results.NoContent(); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsManageRoles));

        // Admin users
        var users = group.MapGroup("/admin-users");

        users.MapGet("/", async ([FromQuery] string? search, [FromQuery] bool? isActive, [FromQuery] int page, [FromQuery] int pageSize, IAdminUserService svc) =>
            Results.Ok(await svc.ListAsync(search, isActive, page <= 0 ? 1 : page, pageSize <= 0 ? 25 : pageSize)))
            .RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsRead));

        users.MapGet("/{id:guid}", async (Guid id, IAdminUserService svc) =>
        {
            var dto = await svc.GetAsync(id);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsRead));

        users.MapPost("/", async ([FromBody] CreateAdminUserRequest req, HttpContext ctx, IAdminUserService svc) =>
        {
            var actor = AdminAuthEndpoints.GetAdminId(ctx)!.Value;
            try { return Results.Ok(await svc.CreateAsync(req, actor)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsCreate));

        users.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateAdminUserRequest req, HttpContext ctx, IAdminUserService svc) =>
        {
            var actor = AdminAuthEndpoints.GetAdminId(ctx)!.Value;
            try { return Results.Ok(await svc.UpdateAsync(id, req, actor)); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsEdit));

        users.MapDelete("/{id:guid}", async (Guid id, HttpContext ctx, IAdminUserService svc) =>
        {
            var actor = AdminAuthEndpoints.GetAdminId(ctx)!.Value;
            try { await svc.DeleteAsync(id, actor); return Results.NoContent(); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsDelete));

        users.MapPost("/{id:guid}/reset-password", async (Guid id, [FromBody] ResetPasswordRequest req, HttpContext ctx, IAdminUserService svc) =>
        {
            var actor = AdminAuthEndpoints.GetAdminId(ctx)!.Value;
            try { await svc.ResetPasswordAsync(id, req.NewPassword, actor); return Results.Ok(new { message = "Password redefinida." }); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsEdit));

        users.MapPost("/{id:guid}/lock", async (Guid id, HttpContext ctx, IAdminUserService svc) =>
        {
            var actor = AdminAuthEndpoints.GetAdminId(ctx)!.Value;
            await svc.LockAsync(id, actor); return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsEdit));

        users.MapPost("/{id:guid}/unlock", async (Guid id, HttpContext ctx, IAdminUserService svc) =>
        {
            var actor = AdminAuthEndpoints.GetAdminId(ctx)!.Value;
            await svc.UnlockAsync(id, actor); return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsEdit));

        users.MapPut("/{id:guid}/roles", async (Guid id, [FromBody] SetAdminRolesRequest req, HttpContext ctx, IAdminUserService svc) =>
        {
            var actor = AdminAuthEndpoints.GetAdminId(ctx)!.Value;
            try { await svc.SetRolesAsync(id, req.RoleIds, actor); return Results.NoContent(); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsAssignPermissions));

        users.MapPut("/{id:guid}/permissions", async (Guid id, [FromBody] SetAdminPermissionOverridesRequest req, HttpContext ctx, IAdminUserService svc) =>
        {
            var actor = AdminAuthEndpoints.GetAdminId(ctx)!.Value;
            try { await svc.SetPermissionOverridesAsync(id, req.Grants, req.Revokes, actor); return Results.NoContent(); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsAssignPermissions));

        users.MapPost("/{id:guid}/promote-super-admin", async (Guid id, HttpContext ctx, IAdminUserService svc) =>
        {
            var actor = AdminAuthEndpoints.GetAdminId(ctx)!.Value;
            try { await svc.PromoteSuperAdminAsync(id, actor); return Results.NoContent(); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsManageSuperAdmin));

        users.MapPost("/{id:guid}/demote-super-admin", async (Guid id, HttpContext ctx, IAdminUserService svc) =>
        {
            var actor = AdminAuthEndpoints.GetAdminId(ctx)!.Value;
            try { await svc.DemoteSuperAdminAsync(id, actor); return Results.NoContent(); }
            catch (Exception ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsManageSuperAdmin));

        users.MapPost("/{id:guid}/revoke-sessions", async (Guid id, HttpContext ctx, IAdminUserService svc) =>
        {
            var actor = AdminAuthEndpoints.GetAdminId(ctx)!.Value;
            await svc.RevokeSessionsAsync(id, actor); return Results.NoContent();
        }).RequireAuthorization(AdminAuthorizationExtensions.PolicyName(PermissionCodes.AdminsRevokeSessions));
    }
}

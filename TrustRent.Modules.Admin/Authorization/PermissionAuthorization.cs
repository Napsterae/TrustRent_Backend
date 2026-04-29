using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Contracts.Interfaces;

namespace TrustRent.Modules.Admin.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Code { get; }
    public PermissionRequirement(string code) => Code = code;
}

public class AdminSessionRequirement : IAuthorizationRequirement;

public class AdminSessionAuthorizationHandler : AuthorizationHandler<AdminSessionRequirement>
{
    private readonly AdminDbContext _db;

    public AdminSessionAuthorizationHandler(AdminDbContext db) => _db = db;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminSessionRequirement requirement)
    {
        if (await ValidateAdminSessionAsync(context.User, _db))
            context.Succeed(requirement);
    }

    internal static async Task<bool> ValidateAdminSessionAsync(ClaimsPrincipal user, AdminDbContext db)
    {
        var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? user.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var adminId)) return false;

        var stamp = user.FindFirst("security_stamp")?.Value;
        var admin = await db.AdminUsers.FindAsync(adminId);
        if (admin is null || admin.DeletedAt != null || !admin.IsActive) return false;
        if (admin.SecurityStamp.ToString() != stamp) return false;

        var jti = user.FindFirst("jti")?.Value;
        if (string.IsNullOrEmpty(jti)) return false;

        var session = await db.Sessions.FirstOrDefaultAsync(s => s.TokenId == jti);
        return session is not null && !session.RevokedAt.HasValue && session.ExpiresAt > DateTime.UtcNow;
    }
}

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissions;
    private readonly AdminDbContext _db;

    public PermissionAuthorizationHandler(IPermissionService permissions, AdminDbContext db)
    {
        _permissions = permissions;
        _db = db;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? context.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var adminId)) return;

        var version = context.User.FindFirst("permissions_version")?.Value;
        if (!await AdminSessionAuthorizationHandler.ValidateAdminSessionAsync(context.User, _db)) return;

        var admin = await _db.AdminUsers.FindAsync(adminId);
        if (admin is null) return;
        if (admin.PermissionsVersion.ToString() != version)
        {
            // Permissions changed: invalidate cache, re-evaluate but force token to be considered stale for hot ops?
            // For now we re-evaluate against fresh data and continue (graceful upgrade).
            _permissions.Invalidate(adminId);
        }

        if (await _permissions.HasPermissionAsync(adminId, requirement.Code))
            context.Succeed(requirement);
    }
}

public static class AdminAuthorizationExtensions
{
    public const string AdminPolicyPrefix = "admin:";

    public static string PolicyName(string code) => AdminPolicyPrefix + code;
}

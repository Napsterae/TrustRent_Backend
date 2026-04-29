using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Contracts.DTOs;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Admin.Models;

namespace TrustRent.Modules.Admin.Services;

public class AdminUserService : IAdminUserService
{
    private readonly AdminDbContext _db;
    private readonly IPermissionService _permissions;
    private readonly IAuditLogService _audit;

    public AdminUserService(AdminDbContext db, IPermissionService permissions, IAuditLogService audit)
    {
        _db = db;
        _permissions = permissions;
        _audit = audit;
    }

    public async Task<PagedResultDto<AdminUserListItemDto>> ListAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.AdminUsers.AsNoTracking().Where(x => x.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower().Trim();
            query = query.Where(x => x.Email.ToLower().Contains(term) || x.Name.ToLower().Contains(term));
        }
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id, x.Email, x.Name, x.IsActive, x.IsSuperAdmin, x.MfaEnabled, x.LastLoginAt,
                Roles = _db.UserRoles.Where(ur => ur.AdminUserId == x.Id)
                                     .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                                     .ToList()
            })
            .ToListAsync(ct);

        var mapped = items.Select(x => new AdminUserListItemDto(x.Id, x.Email, x.Name, x.IsActive, x.IsSuperAdmin, x.MfaEnabled, x.LastLoginAt, x.Roles)).ToList();
        return new PagedResultDto<AdminUserListItemDto>(mapped, page, pageSize, total);
    }

    public async Task<AdminUserDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct);
        if (admin is null) return null;
        var roles = await _db.UserRoles.Where(ur => ur.AdminUserId == id)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name).ToListAsync(ct);
        var perms = await _permissions.GetEffectivePermissionsAsync(id, ct);
        return new AdminUserDto(admin.Id, admin.Email, admin.Name, admin.IsActive, admin.IsSuperAdmin,
            admin.MfaEnabled, admin.MustChangePassword, admin.LastLoginAt, admin.CreatedAt, roles, perms.OrderBy(p => p).ToList());
    }

    public async Task<AdminUserDto> CreateAsync(CreateAdminUserRequest request, Guid actorAdminId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email)) throw new ArgumentException("Email obrigatório.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 12)
            throw new ArgumentException("Password deve ter pelo menos 12 caracteres.");

        var normalized = request.Email.Trim().ToLowerInvariant();
        if (await _db.AdminUsers.AnyAsync(x => x.Email.ToLower() == normalized, ct))
            throw new InvalidOperationException("Já existe um administrador com esse email.");

        var admin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            Name = request.Name?.Trim() ?? string.Empty,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            IsSuperAdmin = request.IsSuperAdmin,
            MustChangePassword = true,
            PasswordChangedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            CreatedByAdminId = actorAdminId
        };
        await _db.AdminUsers.AddAsync(admin, ct);
        await _db.SaveChangesAsync(ct);

        if (request.RoleIds is { Count: > 0 })
        {
            foreach (var rid in request.RoleIds.Distinct())
                _db.UserRoles.Add(new AdminUserRole { AdminUserId = admin.Id, RoleId = rid });
            await _db.SaveChangesAsync(ct);
        }

        await _audit.WriteAsync(actorAdminId, "admins.create", "AdminUser", admin.Id.ToString(), null,
            System.Text.Json.JsonSerializer.Serialize(new { admin.Email, admin.Name, admin.IsSuperAdmin, request.RoleIds }),
            null, null, null, null, ct);

        return (await GetAsync(admin.Id, ct))!;
    }

    public async Task<AdminUserDto> UpdateAsync(Guid id, UpdateAdminUserRequest request, Guid actorAdminId, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct)
            ?? throw new InvalidOperationException("Admin não encontrado.");
        var before = System.Text.Json.JsonSerializer.Serialize(new { admin.Name, admin.IsActive });
        admin.Name = request.Name?.Trim() ?? admin.Name;
        admin.IsActive = request.IsActive;
        admin.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (!request.IsActive)
        {
            await RevokeAllSessionsAsync(id, "deactivated", ct);
        }

        await _audit.WriteAsync(actorAdminId, "admins.update", "AdminUser", id.ToString(), before,
            System.Text.Json.JsonSerializer.Serialize(new { admin.Name, admin.IsActive }), null, null, null, null, ct);

        return (await GetAsync(id, ct))!;
    }

    public async Task DeleteAsync(Guid id, Guid actorAdminId, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct)
            ?? throw new InvalidOperationException("Admin não encontrado.");
        if (id == actorAdminId) throw new InvalidOperationException("Não pode eliminar a sua própria conta.");
        if (admin.IsSuperAdmin)
        {
            var others = await _db.AdminUsers.CountAsync(x => x.IsSuperAdmin && x.DeletedAt == null && x.Id != id, ct);
            if (others == 0) throw new InvalidOperationException("Não é possível eliminar o último super-admin.");
        }
        admin.DeletedAt = DateTime.UtcNow;
        admin.DeletedByAdminId = actorAdminId;
        admin.IsActive = false;
        await _db.SaveChangesAsync(ct);
        await RevokeAllSessionsAsync(id, "deleted", ct);

        await _audit.WriteAsync(actorAdminId, "admins.delete", "AdminUser", id.ToString(), null, null, null, null, null, null, ct);
    }

    public async Task ResetPasswordAsync(Guid id, string newPassword, Guid actorAdminId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 12)
            throw new ArgumentException("Password deve ter pelo menos 12 caracteres.");
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct)
            ?? throw new InvalidOperationException("Admin não encontrado.");
        admin.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        admin.MustChangePassword = true;
        admin.PasswordChangedAt = DateTime.UtcNow;
        admin.SecurityStamp = Guid.NewGuid();
        await _db.SaveChangesAsync(ct);
        await RevokeAllSessionsAsync(id, "password_reset", ct);
        await _audit.WriteAsync(actorAdminId, "admins.reset_password", "AdminUser", id.ToString(), null, null, null, null, null, null, ct);
    }

    public async Task LockAsync(Guid id, Guid actorAdminId, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Admin não encontrado.");
        admin.LockedUntil = DateTime.UtcNow.AddYears(100);
        await _db.SaveChangesAsync(ct);
        await RevokeAllSessionsAsync(id, "locked", ct);
        await _audit.WriteAsync(actorAdminId, "admins.lock", "AdminUser", id.ToString(), null, null, null, null, null, null, ct);
    }

    public async Task UnlockAsync(Guid id, Guid actorAdminId, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Admin não encontrado.");
        admin.LockedUntil = null;
        admin.FailedAttempts = 0;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync(actorAdminId, "admins.unlock", "AdminUser", id.ToString(), null, null, null, null, null, null, ct);
    }

    public async Task SetRolesAsync(Guid id, IEnumerable<Guid> roleIds, Guid actorAdminId, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct)
            ?? throw new InvalidOperationException("Admin não encontrado.");
        var existing = _db.UserRoles.Where(ur => ur.AdminUserId == id);
        _db.UserRoles.RemoveRange(existing);
        var validRoles = (await _db.Roles.Select(r => r.Id).ToListAsync(ct)).ToHashSet();
        foreach (var rid in roleIds.Distinct())
        {
            if (!validRoles.Contains(rid)) continue;
            _db.UserRoles.Add(new AdminUserRole { AdminUserId = id, RoleId = rid });
        }
        await _db.SaveChangesAsync(ct);
        await BumpSecurityAsync(admin, ct);
        await _audit.WriteAsync(actorAdminId, "admins.set_roles", "AdminUser", id.ToString(), null,
            System.Text.Json.JsonSerializer.Serialize(new { roleIds }), null, null, null, null, ct);
    }

    public async Task SetPermissionOverridesAsync(Guid id, IEnumerable<string> grants, IEnumerable<string> revokes, Guid actorAdminId, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct)
            ?? throw new InvalidOperationException("Admin não encontrado.");
        var validCodes = (await _db.Permissions.Select(p => p.Code).ToListAsync(ct)).ToHashSet();
        var existing = _db.UserPermissions.Where(p => p.AdminUserId == id);
        _db.UserPermissions.RemoveRange(existing);
        foreach (var g in grants.Distinct())
            if (validCodes.Contains(g))
                _db.UserPermissions.Add(new AdminUserPermission { AdminUserId = id, PermissionCode = g, IsGrant = true });
        foreach (var r in revokes.Distinct())
            if (validCodes.Contains(r))
                _db.UserPermissions.Add(new AdminUserPermission { AdminUserId = id, PermissionCode = r, IsGrant = false });
        await _db.SaveChangesAsync(ct);
        await BumpSecurityAsync(admin, ct);
        await _audit.WriteAsync(actorAdminId, "admins.set_overrides", "AdminUser", id.ToString(), null,
            System.Text.Json.JsonSerializer.Serialize(new { grants, revokes }), null, null, null, null, ct);
    }

    public async Task PromoteSuperAdminAsync(Guid id, Guid actorAdminId, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct)
            ?? throw new InvalidOperationException("Admin não encontrado.");
        admin.IsSuperAdmin = true;
        await _db.SaveChangesAsync(ct);
        await BumpSecurityAsync(admin, ct);
        await _audit.WriteAsync(actorAdminId, "admins.promote_super", "AdminUser", id.ToString(), null, null, null, null, null, null, ct);
    }

    public async Task DemoteSuperAdminAsync(Guid id, Guid actorAdminId, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null, ct)
            ?? throw new InvalidOperationException("Admin não encontrado.");
        var others = await _db.AdminUsers.CountAsync(x => x.IsSuperAdmin && x.DeletedAt == null && x.Id != id, ct);
        if (others == 0) throw new InvalidOperationException("Não é possível remover o último super-admin.");
        admin.IsSuperAdmin = false;
        await _db.SaveChangesAsync(ct);
        await BumpSecurityAsync(admin, ct);
        await _audit.WriteAsync(actorAdminId, "admins.demote_super", "AdminUser", id.ToString(), null, null, null, null, null, null, ct);
    }

    public async Task RevokeSessionsAsync(Guid id, Guid actorAdminId, CancellationToken ct = default)
    {
        await RevokeAllSessionsAsync(id, "manual_revoke", ct);
        await _audit.WriteAsync(actorAdminId, "admins.revoke_sessions", "AdminUser", id.ToString(), null, null, null, null, null, null, ct);
    }

    private async Task RevokeAllSessionsAsync(Guid adminUserId, string reason, CancellationToken ct)
    {
        var sessions = await _db.Sessions.Where(s => s.AdminUserId == adminUserId && s.RevokedAt == null).ToListAsync(ct);
        foreach (var s in sessions) { s.RevokedAt = DateTime.UtcNow; s.RevokedReason = reason; }
        if (sessions.Count > 0) await _db.SaveChangesAsync(ct);
    }

    private async Task BumpSecurityAsync(AdminUser admin, CancellationToken ct)
    {
        admin.PermissionsVersion++;
        admin.SecurityStamp = Guid.NewGuid();
        await _db.SaveChangesAsync(ct);
        _permissions.Invalidate(admin.Id);
    }
}

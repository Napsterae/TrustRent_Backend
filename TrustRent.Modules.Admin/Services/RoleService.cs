using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Contracts.DTOs;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Admin.Models;

namespace TrustRent.Modules.Admin.Services;

public class RoleService : IRoleService
{
    private readonly AdminDbContext _db;
    private readonly IAuditLogService _audit;

    public RoleService(AdminDbContext db, IAuditLogService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<AdminRoleDto>> ListAsync(CancellationToken ct = default)
    {
        var roles = await _db.Roles.AsNoTracking()
            .Include(r => r.Permissions)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
        return roles.Select(MapToDto).ToList();
    }

    public async Task<AdminRoleDto> CreateAsync(CreateOrUpdateRoleRequest request, Guid actorAdminId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Nome obrigatório.");
        var exists = await _db.Roles.AnyAsync(r => r.Name == request.Name, ct);
        if (exists) throw new InvalidOperationException("Já existe uma role com esse nome.");

        var role = new AdminRole
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description,
            IsSystem = false
        };
        await _db.Roles.AddAsync(role, ct);
        await _db.SaveChangesAsync(ct);

        await ReplacePermissionsAsync(role.Id, request.Permissions, ct);

        await _audit.WriteAsync(actorAdminId, "roles.create", "AdminRole", role.Id.ToString(), null,
            System.Text.Json.JsonSerializer.Serialize(new { role.Name, request.Permissions }), null, null, null, null, ct);

        return (await ListAsync(ct)).First(r => r.Id == role.Id);
    }

    public async Task<AdminRoleDto> UpdateAsync(Guid id, CreateOrUpdateRoleRequest request, Guid actorAdminId, CancellationToken ct = default)
    {
        var role = await _db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new InvalidOperationException("Role não encontrada.");
        if (role.IsSystem) throw new InvalidOperationException("Roles de sistema não podem ser editadas.");

        var before = System.Text.Json.JsonSerializer.Serialize(new { role.Name, role.Description, Perms = role.Permissions.Select(p => p.PermissionCode) });

        role.Name = request.Name.Trim();
        role.Description = request.Description;
        await _db.SaveChangesAsync(ct);
        await ReplacePermissionsAsync(role.Id, request.Permissions, ct);

        var after = System.Text.Json.JsonSerializer.Serialize(new { role.Name, role.Description, Perms = request.Permissions });
        await _audit.WriteAsync(actorAdminId, "roles.update", "AdminRole", role.Id.ToString(), before, after, null, null, null, null, ct);

        return (await ListAsync(ct)).First(r => r.Id == role.Id);
    }

    public async Task DeleteAsync(Guid id, Guid actorAdminId, CancellationToken ct = default)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new InvalidOperationException("Role não encontrada.");
        if (role.IsSystem) throw new InvalidOperationException("Roles de sistema não podem ser eliminadas.");

        var hasMembers = await _db.UserRoles.AnyAsync(ur => ur.RoleId == id, ct);
        if (hasMembers) throw new InvalidOperationException("Existem administradores com esta role atribuída.");

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync(actorAdminId, "roles.delete", "AdminRole", id.ToString(), null, null, null, null, null, null, ct);
    }

    private async Task ReplacePermissionsAsync(Guid roleId, IEnumerable<string> permissions, CancellationToken ct)
    {
        var existing = _db.RolePermissions.Where(rp => rp.RoleId == roleId);
        _db.RolePermissions.RemoveRange(existing);
        var validCodes = (await _db.Permissions.AsNoTracking().Select(p => p.Code).ToListAsync(ct)).ToHashSet();
        foreach (var code in permissions.Distinct())
        {
            if (!validCodes.Contains(code)) continue;
            _db.RolePermissions.Add(new AdminRolePermission { RoleId = roleId, PermissionCode = code });
        }
        await _db.SaveChangesAsync(ct);
    }

    private static AdminRoleDto MapToDto(AdminRole r)
        => new(r.Id, r.Name, r.Description, r.IsSystem, r.Permissions.Select(p => p.PermissionCode).OrderBy(c => c).ToList());
}

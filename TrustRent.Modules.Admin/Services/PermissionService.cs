using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Contracts.Interfaces;

namespace TrustRent.Modules.Admin.Services;

public class PermissionService : IPermissionService
{
    private readonly AdminDbContext _db;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public PermissionService(AdminDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    private static string Key(Guid id) => $"admin:perms:{id}";

    public async Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid adminUserId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue<HashSet<string>>(Key(adminUserId), out var cached) && cached is not null) return cached;

        var admin = await _db.AdminUsers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == adminUserId, ct);
        if (admin is null) return Array.Empty<string>();

        if (admin.IsSuperAdmin)
        {
            var allCodes = await _db.Permissions.AsNoTracking().Select(x => x.Code).ToListAsync(ct);
            var allSet = new HashSet<string>(allCodes, StringComparer.Ordinal);
            _cache.Set(Key(adminUserId), allSet, CacheTtl);
            return allSet;
        }

        var rolePermissions = await _db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.AdminUserId == adminUserId)
            .Join(_db.RolePermissions.AsNoTracking(), ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp.PermissionCode)
            .ToListAsync(ct);

        var overrides = await _db.UserPermissions.AsNoTracking()
            .Where(p => p.AdminUserId == adminUserId)
            .ToListAsync(ct);

        var set = new HashSet<string>(rolePermissions, StringComparer.Ordinal);
        foreach (var ov in overrides)
        {
            if (ov.IsGrant) set.Add(ov.PermissionCode);
            else set.Remove(ov.PermissionCode);
        }

        _cache.Set(Key(adminUserId), set, CacheTtl);
        return set;
    }

    public async Task<bool> HasPermissionAsync(Guid adminUserId, string permissionCode, CancellationToken ct = default)
    {
        var set = await GetEffectivePermissionsAsync(adminUserId, ct);
        return set.Contains(permissionCode);
    }

    public void Invalidate(Guid adminUserId) => _cache.Remove(Key(adminUserId));

    public async Task BumpPermissionsVersionAsync(Guid adminUserId, CancellationToken ct = default)
    {
        var admin = await _db.AdminUsers.FirstOrDefaultAsync(x => x.Id == adminUserId, ct);
        if (admin is null) return;
        admin.PermissionsVersion++;
        admin.SecurityStamp = Guid.NewGuid();
        await _db.SaveChangesAsync(ct);
        Invalidate(adminUserId);
    }
}

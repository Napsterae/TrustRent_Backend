using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Admin.Contracts;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Contracts.DTOs;
using TrustRent.Modules.Admin.Contracts.Interfaces;

namespace TrustRent.Modules.Admin.Services;

public class PermissionCatalogService : IPermissionCatalogService
{
    private readonly AdminDbContext _db;
    public PermissionCatalogService(AdminDbContext db) => _db = db;

    public async Task<IReadOnlyList<PermissionDto>> GetCatalogAsync(CancellationToken ct = default)
    {
        var perms = await _db.Permissions.AsNoTracking()
            .OrderBy(p => p.Category).ThenBy(p => p.Code)
            .ToListAsync(ct);
        return perms.Select(p => new PermissionDto(p.Code, p.Description, p.Category)).ToList();
    }
}

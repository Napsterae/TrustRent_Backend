using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Api.Services;

public class CatalogUserContactAccessService : IUserContactAccessService
{
    private readonly CatalogDbContext _catalogDbContext;

    public CatalogUserContactAccessService(CatalogDbContext catalogDbContext)
    {
        _catalogDbContext = catalogDbContext;
    }

    public async Task<bool> CanViewDirectContactAsync(Guid viewerUserId, Guid targetUserId)
    {
        if (viewerUserId == Guid.Empty || targetUserId == Guid.Empty)
            return false;

        if (viewerUserId == targetUserId)
            return true;

        var hasSharedLease = await _catalogDbContext.Leases.AnyAsync(lease =>
            lease.Status != LeaseStatus.Cancelled &&
            ((lease.TenantId == viewerUserId && lease.LandlordId == targetUserId)
            || (lease.TenantId == targetUserId && lease.LandlordId == viewerUserId)));

        if (hasSharedLease)
            return true;

        return await _catalogDbContext.Applications
            .Join(
                _catalogDbContext.Properties,
                application => application.PropertyId,
                property => property.Id,
                (application, property) => new { application, property })
            .AnyAsync(entry =>
                entry.application.Status != ApplicationStatus.Rejected &&
                ((entry.application.TenantId == viewerUserId && entry.property.LandlordId == targetUserId)
                || (entry.application.TenantId == targetUserId && entry.property.LandlordId == viewerUserId)));
    }
}
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Api.Services;

public class CatalogUserContactAccessService : IUserContactAccessService
{
    private readonly CatalogDbContext _catalogDbContext;
    private readonly LeasingDbContext _leasingDbContext;

    public CatalogUserContactAccessService(CatalogDbContext catalogDbContext, LeasingDbContext leasingDbContext)
    {
        _catalogDbContext = catalogDbContext;
        _leasingDbContext = leasingDbContext;
    }

    public async Task<bool> CanViewDirectContactAsync(Guid viewerUserId, Guid targetUserId)
    {
        if (viewerUserId == Guid.Empty || targetUserId == Guid.Empty)
            return false;

        if (viewerUserId == targetUserId)
            return true;

        var hasSharedLease = await _leasingDbContext.Leases.AnyAsync(lease =>
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
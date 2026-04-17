using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Mappers;
using TrustRent.Shared.Contracts.DTOs;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Api.Services;

/// <summary>
/// Cross-module bridge: allows the Catalog module (ApplicationService)
/// to read Lease data from the Leasing module.
/// </summary>
public class LeasingAccessService : ILeasingAccessService
{
    private readonly LeasingDbContext _leasingDb;

    public LeasingAccessService(LeasingDbContext leasingDb)
    {
        _leasingDb = leasingDb;
    }

    public async Task<LeaseDto?> GetLeaseByApplicationIdAsync(Guid applicationId)
    {
        var lease = await _leasingDb.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.ApplicationId == applicationId);

        return lease?.ToDto();
    }

    public async Task<Dictionary<Guid, LeaseDto>> GetLeasesByApplicationIdsAsync(IEnumerable<Guid> applicationIds)
    {
        var ids = applicationIds.ToList();
        var leases = await _leasingDb.Leases
            .Include(l => l.History)
            .Where(l => ids.Contains(l.ApplicationId))
            .ToListAsync();

        return leases.ToDictionary(l => l.ApplicationId, l => l.ToDto());
    }

    public async Task<List<LeaseDto>> GetLeasesByPropertyIdAsync(Guid propertyId)
    {
        var leases = await _leasingDb.Leases
            .Include(l => l.History)
            .Where(l => l.PropertyId == propertyId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return leases.Select(l => l.ToDto()).ToList();
    }
}

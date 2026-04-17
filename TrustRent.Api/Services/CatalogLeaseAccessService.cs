using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Models;

namespace TrustRent.Api.Services;

public class CatalogLeaseAccessService : ILeaseAccessService
{
    private readonly LeasingDbContext _leasingDbContext;

    public CatalogLeaseAccessService(LeasingDbContext leasingDbContext)
    {
        _leasingDbContext = leasingDbContext;
    }

    public async Task<LeaseAccessContext?> GetLeaseAccessContextAsync(Guid leaseId)
    {
        return await _leasingDbContext.Leases
            .Where(l => l.Id == leaseId)
            .Select(l => new LeaseAccessContext
            {
                LeaseId = l.Id,
                TenantId = l.TenantId,
                LandlordId = l.LandlordId,
                PropertyId = l.PropertyId,
                MonthlyRent = l.MonthlyRent,
                Deposit = l.Deposit,
                AdvanceRentMonths = l.AdvanceRentMonths,
                LeaseStatus = l.Status.ToString()
            })
            .FirstOrDefaultAsync();
    }
}

using TrustRent.Shared.Contracts.Models;

namespace TrustRent.Shared.Contracts.Interfaces;

public interface ILeaseAccessService
{
    Task<LeaseAccessContext?> GetLeaseAccessContextAsync(Guid leaseId);
}

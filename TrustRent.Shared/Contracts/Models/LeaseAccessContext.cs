namespace TrustRent.Shared.Contracts.Models;

public class LeaseAccessContext
{
    public Guid LeaseId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LandlordId { get; set; }
}

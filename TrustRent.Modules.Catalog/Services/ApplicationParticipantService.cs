using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Catalog.Services;

public class ApplicationParticipantService : IApplicationParticipantService
{
    public bool IsPrincipalTenant(Application application, Guid userId)
        => application.TenantId == userId;

    public bool IsCoTenant(Application application, Guid userId)
        => application.CoTenantUserId.HasValue && application.CoTenantUserId.Value == userId;

    public bool IsTenantSide(Application application, Guid userId)
        => IsPrincipalTenant(application, userId) || IsCoTenant(application, userId);

    public bool IsGuarantor(Application application, Guid userId)
    {
        if (application.Guarantors == null || application.Guarantors.Count == 0) return false;
        return application.Guarantors.Any(g =>
            g.UserId.HasValue &&
            g.UserId.Value == userId &&
            (g.InviteStatus == GuarantorInviteStatus.Accepted));
    }

    public bool IsLandlord(Application application, Guid landlordId, Guid userId)
        => landlordId == userId;

    public bool IsParticipant(Application application, Guid landlordId, Guid userId, bool includeGuarantor = true)
        => IsTenantSide(application, userId)
           || IsLandlord(application, landlordId, userId)
           || (includeGuarantor && IsGuarantor(application, userId));
}

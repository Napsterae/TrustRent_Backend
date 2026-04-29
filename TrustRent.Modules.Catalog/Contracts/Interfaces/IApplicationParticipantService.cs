using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

/// <summary>
/// Helper centralizado de autorização para candidaturas multi-parte.
/// Determina se um utilizador é parte legítima de uma candidatura
/// (candidato principal, co-candidato ativo, fiador ativo, ou senhorio do imóvel).
/// </summary>
public interface IApplicationParticipantService
{
    bool IsPrincipalTenant(Application application, Guid userId);
    bool IsCoTenant(Application application, Guid userId);
    bool IsTenantSide(Application application, Guid userId);          // tenant principal OU co-tenant
    bool IsGuarantor(Application application, Guid userId);
    bool IsLandlord(Application application, Guid landlordId, Guid userId);
    bool IsParticipant(Application application, Guid landlordId, Guid userId, bool includeGuarantor = true);
}

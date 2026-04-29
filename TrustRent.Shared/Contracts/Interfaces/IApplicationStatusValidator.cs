namespace TrustRent.Shared.Contracts.Interfaces;

public interface IApplicationStatusValidator
{
    Task<bool> IsApplicationChatLockedAsync(Guid applicationId);
    Task<(Guid TenantId, Guid LandlordId, Guid? CoTenantUserId)?> GetApplicationParticipantsAsync(Guid applicationId);
}

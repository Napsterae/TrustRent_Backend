namespace TrustRent.Shared.Contracts.Interfaces;

public interface IApplicationStatusValidator
{
    Task<bool> IsApplicationChatLockedAsync(Guid applicationId);
    Task<(Guid TenantId, Guid LandlordId, Guid? CoTenantUserId)?> GetApplicationParticipantsAsync(Guid applicationId);
    Task<ApplicationChatContext?> GetApplicationChatContextAsync(Guid applicationId);
}

public sealed class ApplicationChatContext
{
    public Guid ApplicationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LandlordId { get; set; }
    public Guid? CoTenantUserId { get; set; }
    public string PropertyTitle { get; set; } = "Candidatura";
}

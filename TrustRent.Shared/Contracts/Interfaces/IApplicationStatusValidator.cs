namespace TrustRent.Shared.Contracts.Interfaces;

public interface IApplicationStatusValidator
{
    Task<bool> IsApplicationChatLockedAsync(Guid applicationId);
}

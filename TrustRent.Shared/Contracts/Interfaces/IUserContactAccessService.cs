namespace TrustRent.Shared.Contracts.Interfaces;

public interface IUserContactAccessService
{
    Task<bool> CanViewDirectContactAsync(Guid viewerUserId, Guid targetUserId);
}
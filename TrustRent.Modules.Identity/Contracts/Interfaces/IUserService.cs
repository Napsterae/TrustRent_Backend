using TrustRent.Modules.Identity.Models;

namespace TrustRent.Modules.Identity.Contracts.Interfaces;

public interface IUserService
{
    Task<User?> GetProfileAsync(Guid userId);
    Task UpdateProfileAsync(Guid userId, UpdateProfileDto request);
    Task UpdatePasswordAsync(Guid userId, string currentPassword, string newPassword);
    Task<string> UpdateAvatarAsync(Guid userId, Stream fileStream, string fileName);
    Task<VerificationResultDto> VerifyDocumentsAsync(Guid userId, Stream? ccFrontStream, string? ccFrontFileName, Stream? ccBackStream, string? ccBackFileName, Stream? noDebtStream, string? noDebtFileName);
}

// DTOs
public record UpdateProfileDto(string Name, string Email, string? Nif, string? CitizenCardNumber, string? Address, string? PostalCode);
public record VerificationResultDto(bool IsIdentityVerified, DateTime? IdentityExpiryDate, bool IsNoDebtVerified, DateTime? NoDebtExpiryDate, int TrustScore);
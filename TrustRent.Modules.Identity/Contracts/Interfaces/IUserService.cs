using TrustRent.Modules.Identity.Models;

namespace TrustRent.Modules.Identity.Contracts.Interfaces;

public interface IUserService
{
    Task<User?> GetProfileAsync(Guid userId);
    Task<UserProfileDto?> GetProfileDtoAsync(Guid userId);
    Task<PublicUserProfileDto?> GetPublicProfileAsync(Guid userId, Guid viewerUserId);
    Task UpdateProfileAsync(Guid userId, UpdateProfileDto request);
    Task UpdatePasswordAsync(Guid userId, string currentPassword, string newPassword);
    Task<string> UpdateAvatarAsync(Guid userId, Stream fileStream, string fileName);
    Task<VerificationResultDto> VerifyDocumentsAsync(Guid userId, Stream? ccFrontStream, string? ccFrontFileName, Stream? ccBackStream, string? ccBackFileName, Stream? noDebtStream, string? noDebtFileName);
}

// DTOs
public record UpdateProfileDto(
    string Name,
    string Email,
    string? Nif,
    string? CitizenCardNumber,
    string? Address,
    string? PostalCode,
    string? PhoneCountryCode,
    string? PhoneNumber
);
public record UserProfileDto(
    Guid Id,
    string Name,
    string Email,
    string? Nif,
    string? CitizenCardNumber,
    string? Address,
    string? PostalCode,
    string? PhoneCountryCode,
    string? PhoneNumber,
    string? ProfilePictureUrl,
    bool IsIdentityVerified,
    DateTime? IdentityExpiryDate,
    bool IsNoDebtVerified,
    DateTime? NoDebtExpiryDate,
    int TrustScore
);
public record VerificationResultDto(
    bool IsIdentityVerified, 
    DateTime? IdentityExpiryDate, 
    bool IsNoDebtVerified, 
    DateTime? NoDebtExpiryDate, 
    int TrustScore,
    string? ExtractedName = null,
    string? ExtractedNif = null,
    string? ExtractedCcNumber = null
);
public record PublicUserProfileDto(
    Guid Id,
    string Name,
    string? Email,
    string? ProfilePictureUrl,
    int TrustScore,
    bool IsIdentityVerified,
    bool IsNoDebtVerified,
    string? PhoneCountryCode,
    string? PhoneNumber
);
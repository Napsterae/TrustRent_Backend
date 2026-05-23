using TrustRent.Modules.Identity.Models;

namespace TrustRent.Modules.Identity.Contracts.Interfaces;

public interface IUserService
{
    Task<User?> GetProfileAsync(Guid userId);
    Task<UserProfileDto?> GetProfileDtoAsync(Guid userId);
    Task<PublicUserProfileDto?> GetPublicProfileAsync(Guid userId, Guid viewerUserId);
    Task UpdateProfileAsync(Guid userId, UpdateProfileDto request);
    Task<PhoneVerificationRequestResult> RequestPhoneNumberVerificationAsync(Guid userId, string? sourceIp, string? userAgent, CancellationToken ct = default);
    Task<PhoneVerificationStatusDto> GetPhoneVerificationStatusAsync(Guid userId, CancellationToken ct = default);
    Task VerifyPhoneNumberAsync(Guid userId, string code, CancellationToken ct = default);
    Task UpdateNotificationPreferencesAsync(Guid userId, UpdateNotificationPreferencesDto request);
    Task UpdatePasswordAsync(Guid userId, string currentPassword, string newPassword);
    Task<string> UpdateAvatarAsync(Guid userId, Stream fileStream, string fileName);
    Task<VerificationResultDto> VerifyDocumentsAsync(Guid userId, Stream? ccFrontStream, string? ccFrontFileName, Stream? ccBackStream, string? ccBackFileName, Stream? noDebtStream, string? noDebtFileName, Stream? addressProofStream, string? addressProofFileName);
    Task<VerificationResultDto> SimulateVerifyCitizenCardAsync(Guid userId);
    Task<VerificationResultDto> SimulateVerifyNoDebtAsync(Guid userId);
    Task UpdateTrustScoreAsync(Guid userId, int newScore);
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
    string? PhoneNumber,
    string? PhoneContactPlatform = null
);
public record UpdateNotificationPreferencesDto(
    bool EmailNotificationsEnabled,
    bool MessagingNotificationsEnabled
);
public record PhoneVerificationRequestResult(
    string Platform,
    string Message,
    DateTime? ExpiresAtUtc,
    string? DeepLinkUrl,
    string? BotUsername,
    bool AwaitingContactShare
);
public record PhoneVerificationStatusDto(
    string Platform,
    bool IsConfigured,
    bool IsPhoneNumberVerified,
    bool HasStartedConversation,
    bool AwaitingContactShare,
    string Message,
    string? DeepLinkUrl,
    DateTime? ExpiresAtUtc,
    string? TelegramUsername
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
    bool IsPhoneNumberVerified,
    DateTime? PhoneNumberVerifiedAt,
    string PhoneContactPlatform,
    string? TelegramUsername,
    DateTime? TelegramLinkedAt,
    string? ProfilePictureUrl,
    bool EmailNotificationsEnabled,
    bool MessagingNotificationsEnabled,
    bool IsIdentityVerified,
    DateTime? IdentityExpiryDate,
    bool IsNoDebtVerified,
    DateTime? NoDebtExpiryDate,
    bool IsAddressVerified,
    DateTime? AddressVerifiedAt,
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
    string? ExtractedCcNumber = null,
    string? ExtractedAddress = null,
    string? ExtractedPostalCode = null,
    bool IsAddressVerified = false,
    DateTime? AddressVerifiedAt = null
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
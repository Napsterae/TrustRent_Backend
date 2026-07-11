namespace TrustRent.Modules.Identity.Models;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    // Dados Pessoais
    public string? Nif { get; set; }
    public string? CitizenCardNumber { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? PhoneCountryCode { get; set; }
    public string? PhoneNumber { get; set; }
    public string PhoneContactPlatform { get; set; } = PhoneContactPlatforms.Telegram;
    public string? PendingPhoneCountryCode { get; set; }
    public string? PendingPhoneNumber { get; set; }
    public string? PendingPhoneContactPlatform { get; set; }
    public bool IsPhoneNumberVerified { get; set; } = false;
    public DateTime? PhoneNumberVerifiedAt { get; set; }
    public string? TelegramChatId { get; set; }
    public string? TelegramUsername { get; set; }
    public DateTime? TelegramLinkedAt { get; set; }
    public string? TelegramPendingVerificationToken { get; set; } // Stores SHA256 hash of the verification token, not the raw token
    public string? TelegramPendingExpectedPhoneNumber { get; set; }
    public DateTime? TelegramPendingVerificationExpiresAt { get; set; }
    public string? TelegramPendingVerificationError { get; set; }
    public string? EmailBlindIndex { get; set; }
    public string? NifBlindIndex { get; set; }
    public string? CitizenCardNumberBlindIndex { get; set; }
    public string? PhoneNumberBlindIndex { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public bool EmailNotificationsEnabled { get; set; } = true;
    public bool WhatsAppNotificationsEnabled { get; set; } = false;

    // Validações de Confiança (Read-Only no Frontend)
    public bool IsIdentityVerified { get; set; } = false;
    public DateTime? IdentityExpiryDate { get; set; }
    public bool IsNoDebtVerified { get; set; } = false;
    public DateTime? NoDebtExpiryDate { get; set; }
    public bool IsAddressVerified { get; set; } = false;
    public DateTime? AddressVerifiedAt { get; set; }
    public int TrustScore { get; set; } = 50;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Stripe
    public string? StripeCustomerId { get; set; }

    // Backoffice / moderação
    public bool IsSuspended { get; set; } = false;
    public DateTime? SuspendedAt { get; set; }
    public string? SuspendedReason { get; set; }
    public Guid? SuspendedByAdminId { get; set; }
    public DateTime? AnonymizedAt { get; set; }
    public Guid? AnonymizedByAdminId { get; set; }
}


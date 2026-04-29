namespace TrustRent.Modules.Admin.Models;

public class AdminUser
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsSuperAdmin { get; set; } = false;

    // MFA (TOTP)
    public string? MfaSecret { get; set; }
    public bool MfaEnabled { get; set; } = false;
    public string? MfaRecoveryCodesHash { get; set; }

    // Password lifecycle
    public bool MustChangePassword { get; set; } = false;
    public DateTime? PasswordChangedAt { get; set; }
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetExpiresAt { get; set; }

    // Permission/session invalidation
    public int PermissionsVersion { get; set; } = 1;
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    // Lockout / login tracking
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public int FailedAttempts { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }

    // Audit columns
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByAdminId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByAdminId { get; set; }

    // Relationships
    public ICollection<AdminUserRole> Roles { get; set; } = new List<AdminUserRole>();
    public ICollection<AdminUserPermission> PermissionOverrides { get; set; } = new List<AdminUserPermission>();
}

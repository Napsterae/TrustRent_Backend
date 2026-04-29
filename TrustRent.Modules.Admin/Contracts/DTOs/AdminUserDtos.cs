namespace TrustRent.Modules.Admin.Contracts.DTOs;

public record AdminUserDto(
    Guid Id,
    string Email,
    string Name,
    bool IsActive,
    bool IsSuperAdmin,
    bool MfaEnabled,
    bool MustChangePassword,
    DateTime? LastLoginAt,
    DateTime CreatedAt,
    List<string> Roles,
    List<string> EffectivePermissions
);

public record AdminUserListItemDto(
    Guid Id,
    string Email,
    string Name,
    bool IsActive,
    bool IsSuperAdmin,
    bool MfaEnabled,
    DateTime? LastLoginAt,
    List<string> Roles
);

public record CreateAdminUserRequest(
    string Email,
    string Name,
    string Password,
    bool IsSuperAdmin,
    List<Guid> RoleIds
);

public record UpdateAdminUserRequest(
    string Name,
    bool IsActive
);

public record SetAdminRolesRequest(List<Guid> RoleIds);

public record SetAdminPermissionOverridesRequest(
    List<string> Grants,
    List<string> Revokes
);

public record AdminLoginRequest(string Email, string Password, string? MfaCode);

public record AdminLoginResponse(
    AdminUserDto AdminUser,
    List<string> Permissions,
    bool IsSuperAdmin,
    int PermissionsVersion,
    bool MfaRequired,
    bool MustChangePassword,
    bool MfaSetupRequired,
    string? CsrfToken
);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ResetPasswordRequest(string NewPassword);
public record EnableMfaRequest(string Code);

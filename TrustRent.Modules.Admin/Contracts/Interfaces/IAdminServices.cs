using TrustRent.Modules.Admin.Contracts.DTOs;
using TrustRent.Modules.Admin.Models;

namespace TrustRent.Modules.Admin.Contracts.Interfaces;

public record AdminLoginResult(
    AdminUser AdminUser,
    string Jwt,
    string Jti,
    DateTime ExpiresAt,
    IReadOnlyCollection<string> Permissions,
    string? CsrfToken,
    bool MfaRequired
);

public interface IAdminAuthService
{
    Task<AdminLoginResult> LoginAsync(string email, string password, string? mfaCode, string? ip, string? userAgent, CancellationToken ct = default);
    Task LogoutAsync(string jti, CancellationToken ct = default);
    Task<AdminUser?> GetByIdAsync(Guid adminUserId, CancellationToken ct = default);
    Task ChangePasswordAsync(Guid adminUserId, string currentPassword, string newPassword, CancellationToken ct = default);
}

public interface IPermissionService
{
    /// <summary>Resolve effective permissions for an admin user (cached).</summary>
    Task<IReadOnlyCollection<string>> GetEffectivePermissionsAsync(Guid adminUserId, CancellationToken ct = default);
    /// <summary>Returns true if the admin has the given permission (super admin bypasses everything).</summary>
    Task<bool> HasPermissionAsync(Guid adminUserId, string permissionCode, CancellationToken ct = default);
    void Invalidate(Guid adminUserId);
    Task BumpPermissionsVersionAsync(Guid adminUserId, CancellationToken ct = default);
}

public interface IAuditLogService
{
    Task WriteAsync(Guid adminUserId, string action, string entityType, string? entityId, string? beforeJson, string? afterJson, string? reason, string? ip, string? userAgent, string? correlationId, CancellationToken ct = default);
}

public interface IAdminUserService
{
    Task<PagedResultDto<AdminUserListItemDto>> ListAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken ct = default);
    Task<AdminUserDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<AdminUserDto> CreateAsync(CreateAdminUserRequest request, Guid actorAdminId, CancellationToken ct = default);
    Task<AdminUserDto> UpdateAsync(Guid id, UpdateAdminUserRequest request, Guid actorAdminId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid actorAdminId, CancellationToken ct = default);
    Task ResetPasswordAsync(Guid id, string newPassword, Guid actorAdminId, CancellationToken ct = default);
    Task LockAsync(Guid id, Guid actorAdminId, CancellationToken ct = default);
    Task UnlockAsync(Guid id, Guid actorAdminId, CancellationToken ct = default);
    Task SetRolesAsync(Guid id, IEnumerable<Guid> roleIds, Guid actorAdminId, CancellationToken ct = default);
    Task SetPermissionOverridesAsync(Guid id, IEnumerable<string> grants, IEnumerable<string> revokes, Guid actorAdminId, CancellationToken ct = default);
    Task PromoteSuperAdminAsync(Guid id, Guid actorAdminId, CancellationToken ct = default);
    Task DemoteSuperAdminAsync(Guid id, Guid actorAdminId, CancellationToken ct = default);
    Task RevokeSessionsAsync(Guid id, Guid actorAdminId, CancellationToken ct = default);
}

public interface IRoleService
{
    Task<IReadOnlyList<AdminRoleDto>> ListAsync(CancellationToken ct = default);
    Task<AdminRoleDto> CreateAsync(CreateOrUpdateRoleRequest request, Guid actorAdminId, CancellationToken ct = default);
    Task<AdminRoleDto> UpdateAsync(Guid id, CreateOrUpdateRoleRequest request, Guid actorAdminId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid actorAdminId, CancellationToken ct = default);
}

public interface IStagingAccessService
{
    Task<IReadOnlyList<StagingAccessUserDto>> ListUsersAsync(CancellationToken ct = default);
    Task<int> CountActiveUsersAsync(CancellationToken ct = default);
    Task<StagingAccessUserDto?> GetUserAsync(string username, CancellationToken ct = default);
    Task<StagingAccessUserDto> CreateUserAsync(CreateStagingAccessUserRequest request, Guid? updatedByAdminId = null, CancellationToken ct = default);
    Task<StagingAccessUserDto> UpdateUserAsync(string username, UpdateStagingAccessUserRequest request, Guid? updatedByAdminId = null, CancellationToken ct = default);
    Task ResetPasswordAsync(string username, string newPassword, Guid? updatedByAdminId = null, CancellationToken ct = default);
    Task DeleteUserAsync(string username, Guid? updatedByAdminId = null, CancellationToken ct = default);
    Task<StagingAccessUserSessionDto?> GetActiveUserAsync(string username, CancellationToken ct = default);
    Task<StagingAccessUserSessionDto?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default);
    Task<bool?> GetSimulationOverrideAsync(CancellationToken ct = default);
    Task<bool> SetSimulationOverrideAsync(bool enabled, Guid? updatedByAdminId = null, CancellationToken ct = default);
}

public interface IPermissionCatalogService
{
    Task<IReadOnlyList<PermissionDto>> GetCatalogAsync(CancellationToken ct = default);
}

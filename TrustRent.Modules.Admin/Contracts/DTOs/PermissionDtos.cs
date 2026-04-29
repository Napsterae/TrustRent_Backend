namespace TrustRent.Modules.Admin.Contracts.DTOs;

public record PermissionDto(string Code, string Description, string Category);

public record AdminRoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    List<string> Permissions
);

public record CreateOrUpdateRoleRequest(
    string Name,
    string? Description,
    List<string> Permissions
);

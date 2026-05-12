namespace TrustRent.Modules.Admin.Contracts.DTOs;

public record StagingAccessUserDto(
    string Username,
    string DisplayName,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record StagingAccessUserSessionDto(
    string Username,
    string DisplayName
);

public record CreateStagingAccessUserRequest(
    string Username,
    string? DisplayName,
    string Password,
    bool IsActive = true
);

public record UpdateStagingAccessUserRequest(
    string DisplayName,
    bool IsActive
);

public record ResetStagingAccessUserPasswordRequest(string Password);

public record UpdateStagingSimulationsRequest(bool Enabled);

public record StagingAccessAdminStateDto(
    bool IsStagingEnvironment,
    bool AccessEnabled,
    bool SimulationsEnabled,
    IReadOnlyList<StagingAccessUserDto> Users
);
namespace TrustRent.Modules.Admin.Contracts.DTOs;

public record AuditLogEntryDto(
    Guid Id,
    Guid AdminUserId,
    string? AdminUserEmail,
    string Action,
    string EntityType,
    string? EntityId,
    string? Reason,
    string? Ip,
    DateTime CreatedAt
);

public record AuditLogDetailDto(
    Guid Id,
    Guid AdminUserId,
    string? AdminUserEmail,
    string Action,
    string EntityType,
    string? EntityId,
    string? BeforeJson,
    string? AfterJson,
    string? Reason,
    string? Ip,
    string? UserAgent,
    string? CorrelationId,
    DateTime CreatedAt
);

namespace TrustRent.Modules.Catalog.Contracts.DTOs;

public record PropertySummaryDto(
    Guid Id,
    string Title,
    string District,
    string Municipality,
    string Parish,
    bool IsPublic,
    bool IsUnderMaintenance,
    string MainImageUrl
);
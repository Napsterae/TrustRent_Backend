namespace TrustRent.Modules.Catalog.Contracts.DTOs;

public record PropertySummaryDto(
    Guid Id,
    string Title,
    string City,
    bool IsAvailable,
    string MainImageUrl
);
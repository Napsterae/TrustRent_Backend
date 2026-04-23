namespace TrustRent.Modules.Catalog.Contracts.DTOs;

public record IncomeValidationResultDto(
    Guid ApplicationId,
    Guid RangeId,
    string RangeCode,
    string RangeLabel,
    DateTime ValidatedAt
);

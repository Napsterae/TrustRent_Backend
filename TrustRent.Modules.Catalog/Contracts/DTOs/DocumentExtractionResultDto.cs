namespace TrustRent.Modules.Catalog.Contracts.DTOs;

public record DocumentExtractionResultDto(
    string? MatrixArticle = null,
    string? PropertyFraction = null,
    string? EnergyClass = null,
    string? EnergyCertNumber = null,
    string? AtRegistrationNumber = null
);
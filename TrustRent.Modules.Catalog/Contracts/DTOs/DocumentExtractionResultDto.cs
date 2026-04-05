namespace TrustRent.Modules.Catalog.Contracts.DTOs;

public record DocumentExtractionResultDto(
    string? MatrixArticle = null,
    string? PropertyFraction = null,
    string? ParishConcelho = null,
    string? EnergyClass = null,
    string? EnergyCertNumber = null,
    string? AtRegistrationNumber = null,
    string? PermanentCertNumber = null,
    string? PermanentCertOffice = null,
    string? LicenseNumber = null,
    string? LicenseDate = null,
    string? LicenseIssuer = null
);
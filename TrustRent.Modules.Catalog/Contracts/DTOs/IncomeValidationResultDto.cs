using TrustRent.Shared.Models;

namespace TrustRent.Modules.Catalog.Contracts.DTOs;

/// <summary>
/// Pacote de ficheiros submetido pelo inquilino para validar rendimentos.
/// O conjunto exigido depende de <see cref="EmploymentType"/>.
/// </summary>
public record IncomeValidationSubmissionDto(
    EmploymentType EmploymentType,
    IReadOnlyList<(Stream Stream, string FileName)> Payslips,
    (Stream Stream, string FileName)? EmployerDeclaration,
    (Stream Stream, string FileName)? ActivityDeclaration);

public record IncomeValidationResultDto(
    Guid ApplicationId,
    Guid RangeId,
    string RangeCode,
    string RangeLabel,
    DateTime ValidatedAt,
    string Method,
    string EmploymentType,
    int PayslipsProvidedCount,
    string? EmployerName,
    string? EmployerNif
);

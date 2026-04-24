using TrustRent.Modules.Catalog.Contracts.DTOs;

namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

public interface IIncomeValidationService
{
    /// <summary>
    /// Configurado nas políticas de upload e na UI: número de recibos exigidos.
    /// </summary>
    int RequiredPayslipCount { get; }

    /// <summary>
    /// Senhorio pede ao inquilino para fazer upload dos recibos antes da aprovação.
    /// Apenas válido quando a candidatura está em InterestConfirmed.
    /// </summary>
    Task RequestValidationAsync(Guid applicationId, Guid landlordId);

    /// <summary>
    /// Inquilino faz upload dos recibos. Os ficheiros são lidos com IA e descartados.
    /// Valida NIF + Nome contra o utilizador, calcula a média do líquido e atribui
    /// uma faixa salarial (SalaryRange) à candidatura. Não persiste valor exato.
    /// </summary>
    Task<IncomeValidationResultDto> ValidatePayslipsAsync(
        Guid applicationId,
        Guid tenantId,
        IReadOnlyList<(Stream Stream, string FileName)> payslips);

    /// <summary>
    /// DEV-ONLY: Marca a candidatura como rendimentos validados sem chamar a IA, atribuindo
    /// a primeira faixa salarial activa. Apenas utilizado em ambiente de desenvolvimento.
    /// </summary>
    Task<IncomeValidationResultDto> SimulateValidationAsync(Guid applicationId, Guid tenantId);
}

using TrustRent.Modules.Catalog.Contracts.DTOs;

namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

public interface IIncomeValidationService
{
    /// <summary>
    /// Número MÁXIMO de recibos aceites na validação. O inquilino pode enviar
    /// menos (1 ou 2) desde que complemente com declaração da entidade empregadora.
    /// </summary>
    int MaxPayslipCount { get; }

    /// <summary>
    /// Número mínimo de recibos para dispensar a declaração da entidade empregadora.
    /// </summary>
    int PayslipsToSkipDeclaration { get; }

    /// <summary>
    /// Senhorio pede ao inquilino para fazer upload dos recibos antes da aprovação.
    /// Apenas válido quando a candidatura está em InterestConfirmed.
    /// </summary>
    Task RequestValidationAsync(Guid applicationId, Guid landlordId);

    /// <summary>
    /// Inquilino faz upload dos documentos. Os ficheiros são lidos com IA e descartados.
    /// Aceita 3 fluxos: 3 recibos; 1-2 recibos + declaração de empregador; declaração de
    /// atividade (Finanças) + recibos verdes para trabalhadores independentes.
    /// </summary>
    Task<IncomeValidationResultDto> ValidateAsync(
        Guid applicationId,
        Guid tenantId,
        IncomeValidationSubmissionDto submission);

    /// <summary>
    /// DEV-ONLY: Marca a candidatura como rendimentos validados sem chamar a IA, atribuindo
    /// a primeira faixa salarial activa. Apenas utilizado em ambiente de desenvolvimento.
    /// O <paramref name="scenario"/> permite simular cada cenário: "employee" (3 recibos),
    /// "employee-declaration" (1 recibo + declaração), "self-employed" (atividade + recibos verdes).
    /// </summary>
    Task<IncomeValidationResultDto> SimulateValidationAsync(Guid applicationId, Guid tenantId, string? scenario = null);
}

using TrustRent.Modules.Catalog.Contracts.DTOs;

namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

public interface IGuarantorService
{
    /// <summary>
    /// Senhorio solicita formalmente que a candidatura tenha fiador.
    /// Requer Property.HasOfficialContract && Property.AcceptsGuarantor.
    /// </summary>
    Task<ApplicationDto> RequestGuarantorAsync(Guid applicationId, Guid landlordId, RequestGuarantorDto dto);

    /// <summary>
    /// Senhorio dispensa a obrigatoriedade de fiador (ex: candidato apresentou alternativa).
    /// </summary>
    Task<ApplicationDto> WaiveGuarantorAsync(Guid applicationId, Guid landlordId, WaiveGuarantorDto dto);

    /// <summary>
    /// Candidato (principal ou co-tenant) convida um fiador externo por email/token.
    /// </summary>
    Task<GuarantorSummaryDto> InviteGuarantorAsync(Guid applicationId, Guid invitingUserId, CreateGuarantorInviteDto dto, string? sourceIp);

    Task<GuarantorSummaryDto> GetByGuestTokenAsync(string token);

    Task<GuarantorSummaryDto> GetByIdForUserAsync(Guid guarantorId, Guid userId);

    Task<GuarantorSummaryDto> AcceptInviteByTokenAsync(string token);

    Task<GuarantorSummaryDto> DeclineInviteByTokenAsync(string token, GuarantorDecisionDto dto);

    Task<GuarantorSummaryDto> SubmitDataByTokenAsync(string token, SubmitGuarantorDataDto dto);

    /// <summary>
    /// Fiador aceita o convite. Faz binding User -> Guarantor.
    /// </summary>
    Task<GuarantorSummaryDto> AcceptInviteAsync(Guid guarantorId, Guid acceptingUserId);

    /// <summary>
    /// Fiador recusa o convite.
    /// </summary>
    Task<GuarantorSummaryDto> DeclineInviteAsync(Guid guarantorId, Guid decliningUserId, GuarantorDecisionDto dto);

    /// <summary>
    /// Fiador submete os seus dados de KYC + rendimento para revisão pelo senhorio.
    /// Em DEV usa simulação; em produção usaria IGeminiDocumentService + IIncomeValidationService.
    /// </summary>
    Task<GuarantorSummaryDto> SubmitDataAsync(Guid guarantorId, Guid submittingUserId, SubmitGuarantorDataDto dto);

    /// <summary>
    /// Senhorio aprova o fiador. Coloca Application.GuarantorRequirementStatus=Approved e desbloqueia
    /// o fluxo da candidatura.
    /// </summary>
    Task<GuarantorSummaryDto> ApproveAsync(Guid guarantorId, Guid landlordId);

    /// <summary>
    /// Senhorio rejeita o fiador. Candidato terá de propor outro ou desistir.
    /// </summary>
    Task<GuarantorSummaryDto> RejectAsync(Guid guarantorId, Guid landlordId, GuarantorDecisionDto dto);

    /// <summary>
    /// Listar fiadores de uma candidatura (acesso restrito a participantes/senhorio).
    /// </summary>
    Task<IEnumerable<GuarantorSummaryDto>> GetForApplicationAsync(Guid applicationId, Guid requesterId);

    /// <summary>
    /// Listar convites Pending direcionados ao utilizador atual.
    /// </summary>
    Task<IEnumerable<GuarantorSummaryDto>> GetPendingInvitesForUserAsync(Guid userId);
}

/// <summary>
/// DTO de submissão de dados do fiador.
/// Em DEV o serviço usa simulação. Em produção espera-se que estes campos sejam preenchidos
/// pelos serviços de OCR/AI.
/// </summary>
public class SubmitGuarantorDataDto
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? TaxNumber { get; set; }
    public string? IdDocumentNumber { get; set; }
    public string? Address { get; set; }
    public string EmploymentType { get; set; } = "Employee"; // Employee | SelfEmployed
    public string IncomeValidationMethod { get; set; } = "Payslips"; // Payslips | PayslipsWithEmployerDeclaration | ActivityWithGreenReceipts
    public int? PayslipsProvidedCount { get; set; }
    public string? EmployerName { get; set; }
    public string? EmployerNif { get; set; }
    public DateTime? EmploymentStartDate { get; set; }
    public bool SimulateIdentityMatch { get; set; } = true;
}

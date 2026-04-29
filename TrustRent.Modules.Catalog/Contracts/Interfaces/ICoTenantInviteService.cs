using TrustRent.Modules.Catalog.Contracts.DTOs;

namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

public interface ICoTenantInviteService
{
    /// <summary>
    /// Cria um convite Pending para co-candidato a partir do candidato principal.
    /// Falha se já existir convite pendente/aceite na candidatura.
    /// </summary>
    Task<CoTenantInviteDto> CreateInviteAsync(Guid applicationId, Guid inviterUserId, CreateCoTenantInviteDto dto, string? sourceIp);

    /// <summary>
    /// Aceitar um convite. Faz binding do CoTenantUserId na Application.
    /// </summary>
    Task<CoTenantInviteDto> AcceptInviteAsync(Guid inviteId, Guid acceptingUserId);

    /// <summary>
    /// Recusar um convite (pelo convidado).
    /// </summary>
    Task<CoTenantInviteDto> DeclineInviteAsync(Guid inviteId, Guid decliningUserId, RespondCoTenantInviteDto dto);

    /// <summary>
    /// Cancelar um convite (pelo emissor).
    /// </summary>
    Task<CoTenantInviteDto> CancelInviteAsync(Guid inviteId, Guid cancellingUserId);

    /// <summary>
    /// Listar convites Pending direcionados ao utilizador atual (para dashboard).
    /// </summary>
    Task<IEnumerable<CoTenantInviteDto>> GetPendingInvitesForUserAsync(Guid userId);

    /// <summary>
    /// Listar convites de uma candidatura (acesso restrito a candidato/senhorio).
    /// </summary>
    Task<IEnumerable<CoTenantInviteDto>> GetInvitesForApplicationAsync(Guid applicationId, Guid requesterId);
}

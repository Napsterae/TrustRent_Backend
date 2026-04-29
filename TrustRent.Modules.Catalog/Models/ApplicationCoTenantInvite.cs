using TrustRent.Shared.Models;

namespace TrustRent.Modules.Catalog.Models;

/// <summary>
/// Convite emitido pelo candidato principal para que outro utilizador registado
/// se junte à candidatura como co-candidato.
/// </summary>
public class ApplicationCoTenantInvite
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Application? Application { get; set; }

    public Guid InviterUserId { get; set; }      // candidato principal
    public string InviteeEmail { get; set; } = string.Empty; // normalizado para minúsculas
    public Guid? InviteeUserId { get; set; }     // resolvido no envio (utilizador existente)

    public CoTenantInviteStatus Status { get; set; } = CoTenantInviteStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

    public string? DeclineReason { get; set; }
    public string? CreatedFromIp { get; set; }
}

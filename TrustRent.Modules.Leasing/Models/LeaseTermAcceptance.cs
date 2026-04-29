using TrustRent.Shared.Models;

namespace TrustRent.Modules.Leasing.Models;

/// <summary>
/// Aceitação individual dos termos pré-contrato por uma das partes obrigadas
/// (candidato principal e, quando exista, co-candidato). Permite calcular se
/// o lease pode avançar para LeaseStartDateConfirmed.
/// </summary>
public class LeaseTermAcceptance
{
    public Guid Id { get; set; }
    public Guid LeaseId { get; set; }
    public Lease? Lease { get; set; }

    public Guid UserId { get; set; }
    public LeaseSignatoryRole Role { get; set; } // Tenant ou CoTenant na v1

    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;
    public string? AcceptedDocumentHash { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

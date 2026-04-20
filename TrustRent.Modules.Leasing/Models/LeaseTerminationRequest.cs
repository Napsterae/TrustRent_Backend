namespace TrustRent.Modules.Leasing.Models;

/// <summary>
/// Pedido de terminação de contrato de arrendamento.
/// Suporta: Denúncia Antecipada (Art. 1098.º CC), Resolução por Incumprimento (Art. 1083.º CC),
/// e Necessidade de Habitação (Art. 1102.º CC).
/// </summary>
public class LeaseTerminationRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeaseId { get; set; }
    public Guid RequestedById { get; set; }

    /// <summary>Tipo: "EarlyTermination", "BreachResolution", "HousingNeed"</summary>
    public string TerminationType { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    // ── Datas calculadas ──
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public int RequiredNoticeDays { get; set; }
    public DateTime EarliestTerminationDate { get; set; }
    public DateTime ProposedTerminationDate { get; set; }

    // ── Regra do 1/3 (denúncia antecipada) ──
    public DateTime OneThirdDate { get; set; }
    public bool HasPassedOneThird { get; set; }

    // ── Indemnização ──
    public decimal? IndemnificationAmount { get; set; }
    public bool IndemnificationRequired { get; set; } = false;
    public string? IndemnificationReason { get; set; }

    // ── Estado: Pending, Accepted, Completed, Cancelled ──
    public string Status { get; set; } = "Pending";
    public DateTime? ProcessedAt { get; set; }
    public string? ProcessedByNote { get; set; }

    // ── Necessidade de Habitação (Task 5) ──
    public bool? DeclaresNoAlternativeHousing { get; set; }
    public string? BeneficiaryRelation { get; set; }
    public string? BeneficiaryName { get; set; }

    // ── Registo legal ──
    public string? RequesterIpAddress { get; set; }
    public string? RequesterUserAgent { get; set; }
}

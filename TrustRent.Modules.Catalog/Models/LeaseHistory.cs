namespace TrustRent.Modules.Catalog.Models;

public class LeaseHistory
{
    public Guid Id { get; set; }
    public Guid LeaseId { get; set; }

    // Quem executou a ação (Guid.Empty = sistema)
    public Guid ActorId { get; set; }

    // Ação executada: "LeaseInitiated", "StartDateConfirmed", "ContractGenerated",
    //   "SignatureRequested", "SignatureConfirmed", "TermsAccepted", "LeaseActivated", "LeaseCancelled"
    public string Action { get; set; } = string.Empty;

    public string? Message { get; set; }

    // Dados adicionais em JSON (ex: datas, referências CMD)
    public string? EventData { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navegação
    public Lease? Lease { get; set; }
}

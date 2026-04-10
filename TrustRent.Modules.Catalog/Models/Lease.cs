namespace TrustRent.Modules.Catalog.Models;

public class Lease
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LandlordId { get; set; }
    public Guid ApplicationId { get; set; }

    // Datas e duração
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DurationMonths { get; set; }
    public bool AllowsRenewal { get; set; }
    public DateTime? RenewalDate { get; set; }

    // Condições financeiras (copiadas da Property no momento da criação)
    public decimal MonthlyRent { get; set; }
    public decimal? Deposit { get; set; }

    // Regime e tipo de contrato
    public string? LeaseRegime { get; set; }
    public string ContractType { get; set; } = "Informal"; // "Official" ou "Informal"

    // Responsabilidades de pagamento (copiadas da Property)
    public string CondominiumFeesPaidBy { get; set; } = "Inquilino";
    public string WaterPaidBy { get; set; } = "Inquilino";
    public string ElectricityPaidBy { get; set; } = "Inquilino";
    public string GasPaidBy { get; set; } = "Inquilino";

    // Documento do contrato (RGPD: reter 10 anos)
    public string? ContractFilePath { get; set; }
    public DateTime? ContractGeneratedAt { get; set; }
    public DateTime? ContractSignedAt { get; set; }

    // Assinaturas
    public bool LandlordSigned { get; set; } = false;
    public DateTime? LandlordSignedAt { get; set; }
    public string? LandlordSignatureRef { get; set; }

    public bool TenantSigned { get; set; } = false;
    public DateTime? TenantSignedAt { get; set; }
    public string? TenantSignatureRef { get; set; }

    // Estado
    public LeaseStatus Status { get; set; } = LeaseStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navegação
    public Property? Property { get; set; }
    public Application? Application { get; set; }
    public List<LeaseHistory> History { get; set; } = new();
}

public enum LeaseStatus
{
    Pending,            // Aguarda confirmação da data de início
    AwaitingSignatures, // Contrato gerado/termos apresentados, aguarda assinatura/aceitação
    Active,             // Ambos assinaram/aceitaram, arrendamento ativo
    Expired,            // Período de arrendamento terminou
    TerminatedEarly,    // Rescindido antes do fim
    Cancelled           // Cancelado durante fase pendente
}

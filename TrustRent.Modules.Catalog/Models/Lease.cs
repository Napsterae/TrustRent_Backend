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
    public int AdvanceRentMonths { get; set; }

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

    // Assinaturas – Upload Sequencial
    public bool LandlordSigned { get; set; } = false;
    public DateTime? LandlordSignedAt { get; set; }
    public string? LandlordSignatureRef { get; set; }
    public string? LandlordSignedFilePath { get; set; }       // PDF assinado pelo senhorio
    public string? LandlordSignatureCertSubject { get; set; } // DN do certificado CMD usado

    public bool TenantSigned { get; set; } = false;
    public DateTime? TenantSignedAt { get; set; }
    public string? TenantSignatureRef { get; set; }
    public string? TenantSignedFilePath { get; set; }         // PDF final com ambas as assinaturas
    public string? TenantSignatureCertSubject { get; set; }

    public bool LandlordSignatureVerified { get; set; } = false;
    public bool TenantSignatureVerified { get; set; } = false;

    // Integridade do documento – proteção contra upload de PDF diferente do gerado
    /// <summary>SHA-256 (base64) do contrato original gerado pela plataforma.</summary>
    public string? ContractFileHash { get; set; }
    /// <summary>SHA-256 (base64) do PDF já assinado pelo senhorio (para validar o upload do inquilino).</summary>
    public string? LandlordSignedFileHash { get; set; }

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
    Pending,                    // Aguarda confirmação da data de início
    AwaitingSignatures,         // (legado) Contrato apresentado, aguarda assinatura/aceitação
    PendingLandlordSignature,   // [NOVO] Senhorio deve assinar e fazer upload do PDF
    PendingTenantSignature,     // [NOVO] Inquilino deve assinar o PDF já assinado pelo senhorio
    SignaturesVerified,         // [NOVO] Ambas as assinaturas verificadas — contrato final guardado
    AwaitingPayment,            // Aguarda pagamento inicial do inquilino
    Active,                     // Arrendamento ativo
    Expired,                    // Período de arrendamento terminou
    TerminatedEarly,            // Rescindido antes do fim
    Cancelled                   // Cancelado durante fase pendente
}

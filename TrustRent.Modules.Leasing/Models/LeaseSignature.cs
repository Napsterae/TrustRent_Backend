using TrustRent.Shared.Models;

namespace TrustRent.Modules.Leasing.Models;

/// <summary>
/// Assinatura individual de uma das partes (senhorio, inquilino, co-inquilino, fiador)
/// num contrato de arrendamento. Substitui a abordagem antiga de campos pares
/// Tenant*/Landlord* na entidade Lease, permitindo até 4 signatários distintos.
/// </summary>
public class LeaseSignature
{
    public Guid Id { get; set; }
    public Guid LeaseId { get; set; }
    public Lease? Lease { get; set; }

    public Guid UserId { get; set; }
    public LeaseSignatoryRole Role { get; set; }
    public int SequenceOrder { get; set; } // 1=Landlord, 2=Tenant, 3=CoTenant, 4=Guarantor

    public bool Signed { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? SignatureRef { get; set; }
    public string? SignedFilePath { get; set; }
    public string? SignedFileHash { get; set; }
    public string? SignatureCertSubject { get; set; }
    public bool SignatureVerified { get; set; }

    public string? SigningIp { get; set; }
    public string? SigningUserAgent { get; set; }
    public string? ChallengeId { get; set; }
    public string? VerificationError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

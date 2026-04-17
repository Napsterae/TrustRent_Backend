using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Leasing.Services;

public static class LeaseValidator
{
    public static void ValidateInitiate(int applicationStatus, DateTime proposedStartDate)
    {
        if (applicationStatus != (int)ApplicationStatus.Accepted)
            throw new InvalidOperationException("Só é possível iniciar um arrendamento em candidaturas com status 'Accepted'.");

        if (proposedStartDate.Date <= DateTime.UtcNow.Date)
            throw new ArgumentException("A data de início deve ser no futuro.");
    }

    public static void ValidateConfirmStartDate(Lease lease, Guid userId, DateTime startDate)
    {
        if (lease.Status != LeaseStatus.Pending)
            throw new InvalidOperationException("Só é possível confirmar a data de início num arrendamento em estado 'Pending'.");

        if (userId != lease.TenantId && userId != lease.LandlordId)
            throw new UnauthorizedAccessException("Apenas o proprietário ou o inquilino podem confirmar a data de início.");

        if (startDate.Date <= DateTime.UtcNow.Date)
            throw new ArgumentException("A data de início deve ser no futuro.");
    }

    public static void ValidateCounterProposeStartDate(Lease lease, Guid userId, DateTime startDate)
    {
        if (lease.Status != LeaseStatus.Pending)
            throw new InvalidOperationException("Só é possível sugerir uma nova data num arrendamento em estado 'Pending'.");

        if (userId != lease.TenantId && userId != lease.LandlordId)
            throw new UnauthorizedAccessException("Apenas o proprietário ou o inquilino podem sugerir datas.");

        if (startDate.Date <= DateTime.UtcNow.Date)
            throw new ArgumentException("A data de início deve ser no futuro.");
    }

    public static void ValidateRequestSignature(Lease lease, Guid userId, string phoneNumber)
    {
        EnsureParticipant(lease, userId);

        if (lease.Status != LeaseStatus.AwaitingSignatures)
            throw new InvalidOperationException("O contrato não está em fase de assinatura.");

        if (lease.ContractType != "Official")
            throw new InvalidOperationException("Assinatura CMD apenas disponível para contratos oficiais.");

        if (string.IsNullOrWhiteSpace(phoneNumber) || !IsValidPortuguesePhone(phoneNumber))
            throw new ArgumentException("Número de telefone português inválido. Formato esperado: +351XXXXXXXXX.");

        if (userId == lease.LandlordId && lease.LandlordSigned)
            throw new InvalidOperationException("O proprietário já assinou este contrato.");

        if (userId == lease.TenantId && lease.TenantSigned)
            throw new InvalidOperationException("O inquilino já assinou este contrato.");
    }

    public static void ValidateConfirmSignature(Lease lease, Guid userId)
    {
        EnsureParticipant(lease, userId);

        if (lease.Status != LeaseStatus.AwaitingSignatures)
            throw new InvalidOperationException("O contrato não está em fase de assinatura.");
    }

    public static void ValidateAcceptTerms(Lease lease, Guid userId)
    {
        EnsureParticipant(lease, userId);

        if (lease.Status != LeaseStatus.AwaitingSignatures)
            throw new InvalidOperationException("O contrato não está em fase de aceitação.");

        if (lease.ContractType != "Informal")
            throw new InvalidOperationException("Aceitação de termos apenas disponível para contratos informais.");

        if (userId == lease.LandlordId && lease.LandlordSigned)
            throw new InvalidOperationException("O proprietário já aceitou os termos.");

        if (userId == lease.TenantId && lease.TenantSigned)
            throw new InvalidOperationException("O inquilino já aceitou os termos.");
    }

    public static void ValidateCancel(Lease lease, Guid userId)
    {
        EnsureParticipant(lease, userId);

        if (lease.Status == LeaseStatus.Active)
            throw new InvalidOperationException("Não é possível cancelar um arrendamento já ativo. Utilize o processo de rescisão.");

        if (lease.Status == LeaseStatus.Cancelled)
            throw new InvalidOperationException("Este arrendamento já foi cancelado.");
    }

    private static void EnsureParticipant(Lease lease, Guid userId)
    {
        if (userId != lease.TenantId && userId != lease.LandlordId)
            throw new UnauthorizedAccessException("Apenas o proprietário ou o inquilino deste arrendamento podem realizar esta ação.");
    }

    private static bool IsValidPortuguesePhone(string phone)
    {
        var normalized = phone.Replace(" ", "").Replace("-", "");
        if (normalized.StartsWith("+351"))
            normalized = normalized[4..];
        return normalized.Length == 9 && normalized.All(char.IsDigit);
    }
}

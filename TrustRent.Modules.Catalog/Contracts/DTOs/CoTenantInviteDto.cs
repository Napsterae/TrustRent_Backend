namespace TrustRent.Modules.Catalog.Contracts.DTOs;

/// <summary>
/// Convite emitido pelo candidato principal a um co-candidato.
/// </summary>
public class CoTenantInviteDto
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid InviterUserId { get; set; }
    public string InviterName { get; set; } = string.Empty;
    public string? InviterAvatarUrl { get; set; }
    public int InviterTrustScore { get; set; }
    public bool InviterIdentityVerified { get; set; }
    public bool InviterNoDebtVerified { get; set; }
    public string InviteeEmail { get; set; } = string.Empty;
    public string InviteeEmailMasked { get; set; } = string.Empty;
    public Guid? InviteeUserId { get; set; }
    public string? InviteeName { get; set; }
    public string? InviteeAvatarUrl { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? DeclineReason { get; set; }

    // Resumo do imóvel — usado nos cards do dashboard do convidado
    public Guid? PropertyId { get; set; }
    public string? PropertyTitle { get; set; }
    public string? PropertyImageUrl { get; set; }
    public string? PropertyAddress { get; set; }
    public decimal? MonthlyRent { get; set; }
    public int DurationMonths { get; set; }
    public string ApplicationMessage { get; set; } = string.Empty;
    public string ApplicationStatus { get; set; } = string.Empty;
}

public record CreateCoTenantInviteDto(string Email);

public record RespondCoTenantInviteDto(string? Reason = null);

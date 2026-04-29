using System.Text.Json;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Shared.Contracts.DTOs;

namespace TrustRent.Modules.Catalog.Mappers;

public static class ApplicationMappers
{
    public static ApplicationDto ToDto(this Application application, Guid landlordId = default, LeaseDto? leaseDto = null)
    {
        List<string> dates = new();
        if (!string.IsNullOrWhiteSpace(application.TenantProposedDates))
        {
            try
            {
                dates = JsonSerializer.Deserialize<List<string>>(application.TenantProposedDates) ?? new();
            }
            catch
            {
                dates = application.TenantProposedDates.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }

        return new ApplicationDto
        {
            Id = application.Id,
            PropertyId = application.PropertyId,
            TenantId = application.TenantId,
            LandlordId = landlordId,
            Message = application.Message,
            ShareProfile = application.ShareProfile,
            WantsVisit = application.WantsVisit,
            DurationMonths = application.DurationMonths,
            TenantProposedDates = dates,
            LandlordProposedDate = application.LandlordProposedDate,
            FinalVisitDate = application.FinalVisitDate,
            Status = application.Status.ToString(),
            CreatedAt = application.CreatedAt,
            History = application.History?.Select(h => new ApplicationHistoryDto
            {
                Id = h.Id,
                ActorId = h.ActorId,
                Action = h.Action,
                Message = h.Message,
                EventData = h.EventData,
                CreatedAt = h.CreatedAt
            }).OrderBy(h => h.CreatedAt).ToList() ?? new(),
            Lease = leaseDto,
            IsIncomeValidationRequested = application.IsIncomeValidationRequested,
            IncomeValidationRequestedAt = application.IncomeValidationRequestedAt,
            IsIncomeVerified = application.IncomeRangeId.HasValue,
            IncomeRangeCode = application.IncomeRange?.Code,
            IncomeRangeLabel = application.IncomeRange?.Label,
            IncomeValidatedAt = application.IncomeValidatedAt,
            EmploymentType = application.EmploymentType?.ToString(),
            IncomeValidationMethod = application.IncomeValidationMethod?.ToString(),
            PayslipsProvidedCount = application.PayslipsProvidedCount,
            EmployerName = application.EmployerName,
            EmployerNif = application.EmployerNif,
            EmploymentStartDate = application.EmploymentStartDate,

            // Co-candidato
            IsJointApplication = application.IsJointApplication,
            CoTenantUserId = application.CoTenantUserId,
            CoTenantJoinedAt = application.CoTenantJoinedAt,
            IsCoTenantIncomeValidationRequested = application.IsCoTenantIncomeValidationRequested,
            CoTenantIncomeValidationRequestedAt = application.CoTenantIncomeValidationRequestedAt,
            IsCoTenantIncomeVerified = application.CoTenantIncomeRangeId.HasValue,
            CoTenantIncomeRangeCode = application.CoTenantIncomeRange?.Code,
            CoTenantIncomeRangeLabel = application.CoTenantIncomeRange?.Label,
            CoTenantIncomeValidatedAt = application.CoTenantIncomeValidatedAt,
            CoTenantEmploymentType = application.CoTenantEmploymentType?.ToString(),
            CoTenantIncomeValidationMethod = application.CoTenantIncomeValidationMethod?.ToString(),
            CoTenantPayslipsProvidedCount = application.CoTenantPayslipsProvidedCount,
            CoTenantEmployerName = application.CoTenantEmployerName,
            CoTenantEmployerNif = application.CoTenantEmployerNif,
            CoTenantEmploymentStartDate = application.CoTenantEmploymentStartDate,
            CoTenantInvites = application.CoTenantInvites?
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new CoTenantInviteDto
                {
                    Id = i.Id,
                    ApplicationId = i.ApplicationId,
                    InviterUserId = i.InviterUserId,
                    InviteeEmail = i.InviteeEmail,
                    InviteeEmailMasked = MaskEmail(i.InviteeEmail),
                    InviteeUserId = i.InviteeUserId,
                    Status = i.Status.ToString(),
                    CreatedAt = i.CreatedAt,
                    ExpiresAt = i.ExpiresAt,
                    RespondedAt = i.RespondedAt,
                    DeclineReason = i.DeclineReason
                })
                .ToList() ?? new(),
            PendingCoTenantInvite = application.CoTenantInvites?
                .Where(i => i.Status == TrustRent.Shared.Models.CoTenantInviteStatus.Pending && i.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(i => i.CreatedAt)
                .Select(i => new CoTenantInviteDto
                {
                    Id = i.Id,
                    ApplicationId = i.ApplicationId,
                    InviterUserId = i.InviterUserId,
                    InviteeEmail = i.InviteeEmail,
                    InviteeEmailMasked = MaskEmail(i.InviteeEmail),
                    InviteeUserId = i.InviteeUserId,
                    Status = i.Status.ToString(),
                    CreatedAt = i.CreatedAt,
                    ExpiresAt = i.ExpiresAt,
                    RespondedAt = i.RespondedAt,
                    DeclineReason = i.DeclineReason
                })
                .FirstOrDefault(),

            // Fiador
            IsGuarantorRequired = application.IsGuarantorRequired,
            GuarantorRequestedAt = application.GuarantorRequestedAt,
            GuarantorRequestNote = application.GuarantorRequestNote,
            GuarantorRequirementStatus = application.GuarantorRequirementStatus.ToString(),
            GuarantorId = application.GuarantorId,
            Guarantors = application.Guarantors?
                .OrderByDescending(g => g.CreatedAt)
                .Select(g => new GuarantorSummaryDto
                {
                    Id = g.Id,
                    ApplicationId = g.ApplicationId,
                    UserId = g.UserId,
                    GuestEmail = g.GuestEmail,
                    GuestEmailMasked = MaskEmail(g.GuestEmail),
                    GuestName = g.GuestName,
                    GuestPhoneNumber = g.GuestPhoneNumber,
                    UserName = g.GuestName ?? "Fiador",
                    InviteStatus = g.InviteStatus.ToString(),
                    RequirementStatus = application.GuarantorRequirementStatus.ToString(),
                    CreatedAt = g.CreatedAt,
                    ExpiresAt = g.ExpiresAt,
                    RespondedAt = g.RespondedAt,
                    IsIdentityVerified = g.IsIdentityVerified,
                    IdentityVerifiedAt = g.IdentityVerifiedAt,
                    IncomeRangeCode = g.IncomeRange?.Code,
                    IncomeRangeLabel = g.IncomeRange?.Label,
                    IncomeValidatedAt = g.IncomeValidatedAt,
                    EmploymentType = g.EmploymentType?.ToString(),
                    IncomeValidationMethod = g.IncomeValidationMethod?.ToString(),
                    PayslipsProvidedCount = g.PayslipsProvidedCount,
                    EmployerName = g.EmployerName,
                    EmployerNifMasked = MaskNif(g.EmployerNif),
                    EmploymentStartDate = g.EmploymentStartDate,
                    LandlordRequestNote = g.LandlordRequestNote,
                    RejectionReason = g.RejectionReason
                })
                .ToList() ?? new()
        };
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return email;
        var at = email.IndexOf('@');
        if (at <= 1) return email;
        var local = email[..at];
        var domain = email[at..];
        var visible = local.Length <= 2 ? local[..1] : local[..2];
        return $"{visible}{new string('*', Math.Max(2, local.Length - 2))}{domain}";
    }

    private static string? MaskNif(string? nif)
    {
        if (string.IsNullOrWhiteSpace(nif)) return null;
        return nif.Length <= 3 ? nif : new string('*', nif.Length - 3) + nif[^3..];
    }
}

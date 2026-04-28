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
            EmploymentStartDate = application.EmploymentStartDate
        };
    }
}

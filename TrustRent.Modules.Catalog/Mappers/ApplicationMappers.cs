using System.Text.Json;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog.Mappers;

public static class ApplicationMappers
{
    public static ApplicationDto ToDto(this Application application, Guid landlordId = default)
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
            Lease = application.Lease?.ToDto()
        };
    }
}

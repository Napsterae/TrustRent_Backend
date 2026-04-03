using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Mappers;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog.Services;

public class ApplicationService : IApplicationService
{
    private readonly CatalogDbContext _context;

    public ApplicationService(CatalogDbContext context)
    {
        _context = context;
    }

    public async Task<ApplicationDto> SubmitApplicationAsync(Guid propertyId, Guid tenantId, SubmitApplicationDto dto)
    {
        var property = await _context.Properties.FindAsync(propertyId);
        if (property == null) throw new Exception("Property not found");

        // Validação: o proprietário não se pode candidatar ao seu próprio imóvel
        if (property.LandlordId == tenantId)
            throw new Exception("Não te podes candidatar a um imóvel do qual és proprietário.");

        var application = new Application
        {
            Id = Guid.NewGuid(),
            PropertyId = propertyId,
            TenantId = tenantId,
            Message = dto.Message,
            ShareProfile = dto.ShareProfile,
            WantsVisit = dto.WantsVisit,
            TenantProposedDates = JsonSerializer.Serialize(dto.SelectedDates),
            Status = ApplicationStatus.Pending
        };

        var history = new ApplicationHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = application.Id,
            ActorId = tenantId,
            Action = "Criada",
            Message = dto.Message,
            EventData = application.TenantProposedDates
        };

        application.History.Add(history);

        _context.Applications.Add(application);
        await _context.SaveChangesAsync();

        return application.ToDto(property.LandlordId);
    }

    public async Task<IEnumerable<ApplicationDto>> GetApplicationsForPropertyAsync(Guid propertyId, Guid landlordId)
    {
        var apps = await _context.Applications
            .Include(a => a.History)
            .Where(a => a.PropertyId == propertyId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return apps.Select(a => a.ToDto(landlordId));
    }

    public async Task<IEnumerable<ApplicationDto>> GetApplicationsForTenantAsync(Guid tenantId)
    {
        var apps = await _context.Applications
            .Include(a => a.History)
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        // For each application, look up the property's LandlordId
        var propertyIds = apps.Select(a => a.PropertyId).Distinct().ToList();
        var properties = await _context.Properties
            .Where(p => propertyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.LandlordId);

        return apps.Select(a => a.ToDto(properties.GetValueOrDefault(a.PropertyId)));
    }

    public async Task<ApplicationDto?> GetApplicationByIdAsync(Guid applicationId, Guid userId)
    {
        var application = await _context.Applications
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application == null) return null;

        var property = await _context.Properties.FindAsync(application.PropertyId);
        var landlordId = property?.LandlordId ?? Guid.Empty;

        return application.ToDto(landlordId);
    }

    public async Task<ApplicationDto> UpdateVisitStatusAsync(Guid applicationId, Guid userId, UpdateApplicationVisitDto dto)
    {
        var application = await _context.Applications.Include(a => a.History).FirstOrDefaultAsync(a => a.Id == applicationId);
        if (application == null) throw new Exception("Application not found");

        var history = new ApplicationHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = application.Id,
            ActorId = userId
        };

        switch (dto.Action)
        {
            case "CounterPropose":
                application.LandlordProposedDate = dto.LandlordProposedDate;
                application.Status = ApplicationStatus.VisitCounterProposed;
                history.Action = "Senhorio Contra-Propôs";
                history.EventData = dto.LandlordProposedDate?.ToString("O");
                break;
            case "AcceptTenantDate":
                if (dto.SelectedTenantDate != null && DateTime.TryParse(dto.SelectedTenantDate, out var dt))
                {
                    application.FinalVisitDate = dt;
                    application.Status = ApplicationStatus.VisitAccepted;
                    history.Action = "Senhorio Aceitou Data do Inquilino";
                    history.EventData = dt.ToString("O");
                }
                break;
            case "AcceptCounterProposal":
                application.FinalVisitDate = application.LandlordProposedDate;
                application.Status = ApplicationStatus.VisitAccepted;
                history.Action = "Inquilino Aceitou Contra-Proposta";
                history.EventData = application.LandlordProposedDate?.ToString("O");
                break;
            case "TenantCounterPropose":
                application.LandlordProposedDate = null;
                application.TenantProposedDates = JsonSerializer.Serialize(dto.TenantProposedDates);
                application.Status = ApplicationStatus.Pending;
                history.Action = "Inquilino Sugeriu Novas Datas";
                history.EventData = application.TenantProposedDates;
                break;
            case "Reject":
                application.Status = ApplicationStatus.Rejected;
                history.Action = "Rejeitada";
                break;
            case "FinalAccept":
                application.Status = ApplicationStatus.Accepted;
                history.Action = "Candidatura Aceite";
                break;
            default:
                throw new Exception("Invalid action");
        }

        application.History.Add(history);
        application.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var property = await _context.Properties.FindAsync(application.PropertyId);
        return application.ToDto(property?.LandlordId ?? Guid.Empty);
    }
}

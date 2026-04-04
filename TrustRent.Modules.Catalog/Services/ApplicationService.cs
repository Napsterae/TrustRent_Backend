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
            ApplicationId = application.Id,
            ActorId = userId
        };

        switch (dto.Action)
        {
            case "CounterPropose":
                application.LandlordProposedDate = dto.LandlordProposedDate?.ToUniversalTime();
                application.Status = ApplicationStatus.VisitCounterProposed;
                history.Action = "Senhorio Contra-Propôs";
                history.EventData = dto.LandlordProposedDate?.ToString("O");
                break;
            case "AcceptTenantDate":
                if (dto.SelectedTenantDate != null && DateTime.TryParse(dto.SelectedTenantDate, out var dt))
                {
                    application.FinalVisitDate = dt.ToUniversalTime();
                    application.Status = ApplicationStatus.VisitAccepted;
                    history.Action = "Senhorio Aceitou Data do Inquilino";
                    history.EventData = dt.ToString("O");
                }
                else
                {
                    throw new Exception("Data de visita inválida ou não selecionada.");
                }
                break;
            case "AcceptCounterProposal":
                application.FinalVisitDate = application.LandlordProposedDate?.ToUniversalTime();
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
                history.Action = "Candidatura Rejeitada";
                break;
            case "TenantConfirmInterest":
                if (application.Status != ApplicationStatus.VisitAccepted)
                    throw new Exception("Tenant can only confirm interest after visit is accepted.");
                application.Status = ApplicationStatus.InterestConfirmed;
                history.Action = "Inquilino Confirmou Interesse";
                history.Message = "O inquilino expressou o desejo de avançar com o aluguer após a visita.";
                break;
                // Em fase final, o senhorio aprova o contrato.
                // Regra: Senhorio só pode aceitar se o inquilino confirmou interesse pós-visita
                if (application.Status != ApplicationStatus.InterestConfirmed && application.Status != ApplicationStatus.Pending)
                    throw new Exception("Final acceptance can only happen after interest confirmation.");
                
                application.Status = ApplicationStatus.Accepted;
                history.Action = "Candidatura Aprovada (Contrato)";

                // Cancelar outras candidaturas ativas para o mesmo imóvel
                var otherApplications = await _context.Applications
                    .Where(a => a.PropertyId == application.PropertyId && a.Id != application.Id && a.Status != ApplicationStatus.Rejected && a.Status != ApplicationStatus.Accepted)
                    .ToListAsync();
                
                foreach(var otherApp in otherApplications)
                {
                    otherApp.Status = ApplicationStatus.Rejected;
                    otherApp.UpdatedAt = DateTime.UtcNow;
                    _context.ApplicationHistories.Add(new ApplicationHistory
                    {
                        ApplicationId = otherApp.Id,
                        ActorId = userId,
                        Action = "Candidatura Rejeitada Ausente",
                        Message = "Esta candidatura foi automaticamente cancelada porque o imóvel foi arrendado a outro candidato."
                    });
                }
                break;
            default:
                throw new Exception("Invalid action");
        }

        _context.ApplicationHistories.Add(history);
        application.UpdatedAt = DateTime.UtcNow;

        var property = await _context.Properties.FindAsync(application.PropertyId);

        if (application.Status == ApplicationStatus.Accepted && property != null)
        {
            property.TenantId = application.TenantId;
        }

        await _context.SaveChangesAsync();

        return application.ToDto(property?.LandlordId ?? Guid.Empty);
    }
}

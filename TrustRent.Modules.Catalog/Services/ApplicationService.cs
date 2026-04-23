using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Mappers;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Catalog.Services;

public class ApplicationService : IApplicationService
{
    private readonly CatalogDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILeasingAccessService _leasingAccess;
    private readonly IUserService _userService;

    public ApplicationService(CatalogDbContext context, INotificationService notificationService, ILeasingAccessService leasingAccess, IUserService userService)
    {
        _context = context;
        _notificationService = notificationService;
        _leasingAccess = leasingAccess;
        _userService = userService;
    }

    public async Task<ApplicationDto> SubmitApplicationAsync(Guid propertyId, Guid tenantId, SubmitApplicationDto dto)
    {
        var property = await _context.Properties.FindAsync(propertyId);
        if (property == null) throw new Exception("Property not found");

        if (!property.IsPublic)
            throw new Exception("Este imóvel já não está disponível para novas candidaturas.");

        if (property.TenantId.HasValue)
            throw new Exception("Este imóvel já se encontra arrendado e não aceita novas candidaturas.");

        // Validação: o proprietário não se pode candidatar ao seu próprio imóvel
        if (property.LandlordId == tenantId)
            throw new Exception("Não te podes candidatar a um imóvel do qual és proprietário.");

        // Lei do Arrendamento 2026: Habitação Permanente requer duração mínima de 3 anos (36 meses)
        if (property.LeaseRegime == LeaseRegime.PermanentHousing && dto.DurationMonths < 36)
            throw new Exception("Nos termos da Lei do Arrendamento, contratos de Habitação Permanente têm uma duração mínima obrigatória de 3 anos (36 meses).");

        var application = new Application
        {
            Id = Guid.NewGuid(),
            PropertyId = propertyId,
            TenantId = tenantId,
            Message = dto.Message,
            ShareProfile = true,
            WantsVisit = dto.WantsVisit,
            DurationMonths = dto.DurationMonths,
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

        // NOTIFICAR SENHORIO
        await _notificationService.SendNotificationAsync(
            property.LandlordId, 
            "application", 
            $"Recebeste uma nova candidatura para '{property.Title}'.", 
            application.Id);

        return application.ToDto(property.LandlordId);
    }

    public async Task<IEnumerable<ApplicationDto>> GetApplicationsForPropertyAsync(Guid propertyId, Guid landlordId)
    {
        // Verify the caller is actually the landlord of this property
        var property = await _context.Properties.FindAsync(propertyId);
        if (property == null) throw new KeyNotFoundException("Imóvel não encontrado.");
        if (property.LandlordId != landlordId) throw new UnauthorizedAccessException("Não tem permissão para ver as candidaturas deste imóvel.");

        var apps = await _context.Applications
            .Include(a => a.History)
            .Include(a => a.IncomeRange)
            .Where(a => a.PropertyId == propertyId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var leases = await _leasingAccess.GetLeasesByApplicationIdsAsync(apps.Select(a => a.Id));
        await ReconcileAwaitingPaymentAsync(apps, leases);

        var tenantIds = apps
            .Select(a => a.TenantId)
            .Distinct()
            .ToList();

        var tenantInfoById = new Dictionary<Guid, (string Name, string? AvatarUrl, int ReviewScore)>();
        foreach (var tenantId in tenantIds)
        {
            var profile = await _userService.GetPublicProfileAsync(tenantId, landlordId);
            tenantInfoById[tenantId] = (
                profile?.Name ?? "Inquilino",
                profile?.ProfilePictureUrl,
                profile?.TrustScore ?? 0
            );
        }

        var dtos = apps.Select(a =>
        {
            var dto = a.ToDto(landlordId, leases.GetValueOrDefault(a.Id));
            if (tenantInfoById.TryGetValue(a.TenantId, out var tenantInfo))
            {
                dto.TenantName = tenantInfo.Name;
                dto.TenantAvatarUrl = tenantInfo.AvatarUrl;
                dto.TenantReviewScore = tenantInfo.ReviewScore;
            }

            return dto;
        });

        return dtos;
    }

    public async Task<IEnumerable<ApplicationDto>> GetApplicationsForTenantAsync(Guid tenantId)
    {
        var apps = await _context.Applications
            .Include(a => a.History)
            .Include(a => a.IncomeRange)
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        var leases = await _leasingAccess.GetLeasesByApplicationIdsAsync(apps.Select(a => a.Id));
        await ReconcileAwaitingPaymentAsync(apps, leases);

        var propertyIds = apps.Select(a => a.PropertyId).Distinct().ToList();
        var properties = await _context.Properties
            .Where(p => propertyIds.Contains(p.Id))
            .Select(p => new
            {
                p.Id,
                p.LandlordId,
                p.Title,
                MainImageUrl = p.Images.Where(i => i.IsMain).Select(i => i.Url).FirstOrDefault()
                    ?? p.Images.Select(i => i.Url).FirstOrDefault()
            })
            .ToDictionaryAsync(p => p.Id);

        var landlordIds = properties.Values
            .Select(p => p.LandlordId)
            .Distinct()
            .ToList();

        var landlordNames = new Dictionary<Guid, string>();
        foreach (var landlordId in landlordIds)
        {
            var profile = await _userService.GetPublicProfileAsync(landlordId, tenantId);
            landlordNames[landlordId] = profile?.Name ?? "Senhorio";
        }

        var applicationDtos = apps.Select(a =>
        {
            var dto = a.ToDto(properties.GetValueOrDefault(a.PropertyId)?.LandlordId ?? Guid.Empty, leases.GetValueOrDefault(a.Id));

            if (properties.TryGetValue(a.PropertyId, out var propertyInfo))
            {
                dto.PropertyTitle = propertyInfo.Title;
                dto.PropertyImageUrl = propertyInfo.MainImageUrl;
                dto.LandlordName = landlordNames.GetValueOrDefault(propertyInfo.LandlordId, "Senhorio");
            }

            return dto;
        });

        return applicationDtos;
    }

    public async Task<ApplicationDto?> GetApplicationByIdAsync(Guid applicationId, Guid userId)
    {
        var application = await _context.Applications
            .Include(a => a.History)
            .Include(a => a.IncomeRange)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application == null) return null;

        // Verify the user is either the tenant or the landlord of this property
        var property = await _context.Properties.FindAsync(application.PropertyId);
        var landlordId = property?.LandlordId ?? Guid.Empty;
        
        if (application.TenantId != userId && landlordId != userId)
            throw new UnauthorizedAccessException("Não tem permissão para ver esta candidatura.");

        // Load lease data from Leasing module
        var leaseDto = await _leasingAccess.GetLeaseByApplicationIdAsync(applicationId);

        // Reconciliação automática: se o lease já está Active mas a candidatura ficou presa em AwaitingPayment
        if (application.Status == ApplicationStatus.AwaitingPayment &&
            leaseDto != null &&
            leaseDto.Status == LeaseStatus.Active.ToString())
        {
            application.Status = ApplicationStatus.LeaseActive;
            application.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return application.ToDto(landlordId, leaseDto);
    }

    public async Task<ApplicationDto> UpdateVisitStatusAsync(Guid applicationId, Guid userId, UpdateApplicationVisitDto dto)
    {
        var application = await _context.Applications
            .Include(a => a.Property)
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == applicationId);
            
        if (application == null) throw new Exception("Application not found");
        var property = application.Property;
        if (property == null) throw new Exception("Property associated with application not found");

        var history = new ApplicationHistory
        {
            ApplicationId = application.Id,
            ActorId = userId
        };

        Guid recipientId = Guid.Empty;
        string notificationMsg = "";

        switch (dto.Action)
        {
            case "CounterPropose":
                application.LandlordProposedDate = dto.LandlordProposedDate?.ToUniversalTime();
                application.Status = ApplicationStatus.VisitCounterProposed;
                history.Action = "Senhorio Contra-Propôs";
                history.EventData = dto.LandlordProposedDate?.ToString("O");
                recipientId = application.TenantId;
                notificationMsg = "O senhorio propôs uma nova data de visita.";
                break;
            case "AcceptTenantDate":
                if (dto.SelectedTenantDate != null && DateTime.TryParse(dto.SelectedTenantDate, out var dt))
                {
                    application.FinalVisitDate = dt.ToUniversalTime();
                    application.Status = ApplicationStatus.VisitAccepted;
                    history.Action = "Senhorio Aceitou Data do Inquilino";
                    history.EventData = dt.ToString("O");
                    recipientId = application.TenantId;
                    notificationMsg = "O senhorio aceitou a tua data de visita!";
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
                recipientId = property.LandlordId;
                notificationMsg = "O inquilino aceitou a contra-proposta de visita.";
                break;
            case "TenantCounterPropose":
                application.LandlordProposedDate = null;
                application.TenantProposedDates = JsonSerializer.Serialize(dto.TenantProposedDates);
                application.Status = ApplicationStatus.Pending;
                history.Action = "Inquilino Sugeriu Novas Datas";
                history.EventData = application.TenantProposedDates;
                recipientId = property.LandlordId;
                notificationMsg = "O inquilino sugeriu novas datas de visita.";
                break;
            case "Reject":
                application.Status = ApplicationStatus.Rejected;
                history.Action = "Candidatura Rejeitada";
                recipientId = application.TenantId;
                notificationMsg = "A tua candidatura foi rejeitada.";
                break;
            case "TenantConfirmInterest":
                if (application.Status != ApplicationStatus.VisitAccepted)
                    throw new Exception("Tenant can only confirm interest after visit is accepted.");
                application.Status = ApplicationStatus.InterestConfirmed;
                history.Action = "Inquilino Confirmou Interesse";
                history.Message = "O inquilino expressou o desejo de avançar com o aluguer após a visita.";
                recipientId = property.LandlordId;
                notificationMsg = "O inquilino confirmou interesse após a visita!";
                break;
            case "Accepted":
                if (application.Status != ApplicationStatus.InterestConfirmed
                    && application.Status != ApplicationStatus.Pending
                    && application.Status != ApplicationStatus.IncomeValidationRequested)
                    throw new Exception("Final acceptance can only happen after interest confirmation.");
                
                application.Status = ApplicationStatus.Accepted;
                history.Action = "Candidatura Aprovada (Contrato)";
                recipientId = application.TenantId;
                notificationMsg = "Parabéns! A tua candidatura foi aprovada!";

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

                    // Notificar inquilinos rejeitados
                    await _notificationService.SendNotificationAsync(otherApp.TenantId, "application", "A tua candidatura foi encerrada — o imóvel foi arrendado.", otherApp.Id);
                }
                break;
            default:
                throw new Exception("Invalid action");
        }

        _context.ApplicationHistories.Add(history);
        application.UpdatedAt = DateTime.UtcNow;

        if (application.Status == ApplicationStatus.Accepted && property != null)
        {
            property.TenantId = application.TenantId;
        }

        await _context.SaveChangesAsync();

        // Enviar notificação de mudança de estado (se aplicável)
        if (recipientId != Guid.Empty)
        {
            await _notificationService.SendNotificationAsync(recipientId, "application", notificationMsg, application.Id);
        }

        return application.ToDto(property?.LandlordId ?? Guid.Empty);
    }

    /// <summary>
    /// Reconcilia candidaturas presas em AwaitingPayment quando o lease já está Active.
    /// Isto pode acontecer com dados criados antes do fluxo de pagamento ser implementado.
    /// </summary>
    private async Task ReconcileAwaitingPaymentAsync(List<Application> apps, Dictionary<Guid, TrustRent.Shared.Contracts.DTOs.LeaseDto> leases)
    {
        var needsSave = false;
        foreach (var app in apps)
        {
            if (app.Status == ApplicationStatus.AwaitingPayment &&
                leases.TryGetValue(app.Id, out var lease) &&
                lease.Status == LeaseStatus.Active.ToString())
            {
                app.Status = ApplicationStatus.LeaseActive;
                app.UpdatedAt = DateTime.UtcNow;
                needsSave = true;
            }
        }
        if (needsSave)
        {
            await _context.SaveChangesAsync();
        }
    }
}

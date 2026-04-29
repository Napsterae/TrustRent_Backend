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
    private readonly IUserRepository _userRepository;
    private readonly IServiceProvider _serviceProvider;

    public ApplicationService(CatalogDbContext context, INotificationService notificationService, ILeasingAccessService leasingAccess, IUserService userService, IUserRepository userRepository, IServiceProvider serviceProvider)
    {
        _context = context;
        _notificationService = notificationService;
        _leasingAccess = leasingAccess;
        _userService = userService;
        _userRepository = userRepository;
        _serviceProvider = serviceProvider;
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

        await ValidateInitialCoTenantInviteAsync(property, tenantId, dto.CoTenantEmail);

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

        // Convite inicial de co-candidato (opcional)
        if (!string.IsNullOrWhiteSpace(dto.CoTenantEmail))
        {
            var coTenantInviteService = _serviceProvider.GetService(typeof(ICoTenantInviteService)) as ICoTenantInviteService
                ?? throw new InvalidOperationException("Serviço de convite de co-candidato indisponível.");

            await coTenantInviteService.CreateInviteAsync(
                application.Id,
                tenantId,
                new CreateCoTenantInviteDto(dto.CoTenantEmail.Trim()),
                sourceIp: null);
        }

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
            .Include(a => a.CoTenantIncomeRange)
            .Include(a => a.CoTenantInvites)
            .Include(a => a.Guarantors).ThenInclude(g => g.IncomeRange)
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

            ApplyCurrentUserRole(dto, landlordId);

            return dto;
        }).ToList();

        await HydrateParticipantProfilesAsync(dtos, landlordId);

        return dtos;
    }

    public async Task<IEnumerable<ApplicationDto>> GetApplicationsForTenantAsync(Guid tenantId)
    {
        var apps = await _context.Applications
            .Include(a => a.History)
            .Include(a => a.IncomeRange)
            .Include(a => a.CoTenantIncomeRange)
            .Include(a => a.CoTenantInvites)
            .Include(a => a.Guarantors).ThenInclude(g => g.IncomeRange)
            .Where(a => a.TenantId == tenantId
                        || a.CoTenantUserId == tenantId
                        || a.Guarantors.Any(g => g.UserId == tenantId && g.InviteStatus == GuarantorInviteStatus.Accepted))
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
            ApplyCurrentUserRole(dto, tenantId);

            if (properties.TryGetValue(a.PropertyId, out var propertyInfo))
            {
                dto.PropertyTitle = propertyInfo.Title;
                dto.PropertyImageUrl = propertyInfo.MainImageUrl;
                dto.LandlordName = landlordNames.GetValueOrDefault(propertyInfo.LandlordId, "Senhorio");
            }

            return dto;
        }).ToList();

        await HydrateParticipantProfilesAsync(applicationDtos, tenantId);

        return applicationDtos;
    }

    public async Task<ApplicationDto?> GetApplicationByIdAsync(Guid applicationId, Guid userId)
    {
        var application = await _context.Applications
            .Include(a => a.History)
            .Include(a => a.IncomeRange)
            .Include(a => a.CoTenantIncomeRange)
            .Include(a => a.CoTenantInvites)
            .Include(a => a.Guarantors).ThenInclude(g => g.IncomeRange)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application == null) return null;

        var property = await _context.Properties.FindAsync(application.PropertyId);
        var landlordId = property?.LandlordId ?? Guid.Empty;

        // Autorização alargada: candidato principal, co-candidato, fiador aceite ou senhorio
        var isParticipant = application.TenantId == userId
                            || (application.CoTenantUserId.HasValue && application.CoTenantUserId.Value == userId)
                            || landlordId == userId
                            || application.Guarantors.Any(g => g.UserId.HasValue && g.UserId.Value == userId && g.InviteStatus == GuarantorInviteStatus.Accepted);
        if (!isParticipant)
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

        var dto = application.ToDto(landlordId, leaseDto);
        ApplyCurrentUserRole(dto, userId);
        await HydrateParticipantProfilesAsync(new[] { dto }, userId);
        return dto;
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

        var isLandlord = property.LandlordId == userId;
        var isPrincipalTenant = application.TenantId == userId;

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
                if (!isLandlord) throw new UnauthorizedAccessException("Só o senhorio pode propor nova data de visita.");
                application.LandlordProposedDate = dto.LandlordProposedDate?.ToUniversalTime();
                application.Status = ApplicationStatus.VisitCounterProposed;
                history.Action = "Senhorio Contra-Propôs";
                history.EventData = dto.LandlordProposedDate?.ToString("O");
                recipientId = application.TenantId;
                notificationMsg = "O senhorio propôs uma nova data de visita.";
                break;
            case "AcceptTenantDate":
                if (!isLandlord) throw new UnauthorizedAccessException("Só o senhorio pode aceitar uma data proposta.");
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
                if (!isPrincipalTenant) throw new UnauthorizedAccessException("Só o candidato principal pode aceitar contrapropostas de visita.");
                application.FinalVisitDate = application.LandlordProposedDate?.ToUniversalTime();
                application.Status = ApplicationStatus.VisitAccepted;
                history.Action = "Inquilino Aceitou Contra-Proposta";
                history.EventData = application.LandlordProposedDate?.ToString("O");
                recipientId = property.LandlordId;
                notificationMsg = "O inquilino aceitou a contra-proposta de visita.";
                break;
            case "TenantCounterPropose":
                if (!isPrincipalTenant) throw new UnauthorizedAccessException("Só o candidato principal pode sugerir novas datas.");
                application.LandlordProposedDate = null;
                application.TenantProposedDates = JsonSerializer.Serialize(dto.TenantProposedDates);
                application.Status = ApplicationStatus.Pending;
                history.Action = "Inquilino Sugeriu Novas Datas";
                history.EventData = application.TenantProposedDates;
                recipientId = property.LandlordId;
                notificationMsg = "O inquilino sugeriu novas datas de visita.";
                break;
            case "Reject":
                if (!isLandlord) throw new UnauthorizedAccessException("Só o senhorio pode rejeitar a candidatura.");
                application.Status = ApplicationStatus.Rejected;
                history.Action = "Candidatura Rejeitada";
                recipientId = application.TenantId;
                notificationMsg = "A tua candidatura foi rejeitada.";
                break;
            case "TenantConfirmInterest":
                if (!isPrincipalTenant) throw new UnauthorizedAccessException("Só o candidato principal pode confirmar interesse.");
                if (application.Status != ApplicationStatus.VisitAccepted)
                    throw new Exception("Tenant can only confirm interest after visit is accepted.");
                application.Status = ApplicationStatus.InterestConfirmed;
                history.Action = "Inquilino Confirmou Interesse";
                history.Message = "O inquilino expressou o desejo de avançar com o aluguer após a visita.";
                recipientId = property.LandlordId;
                notificationMsg = "O inquilino confirmou interesse após a visita!";
                break;
            case "Accepted":
                if (!isLandlord) throw new UnauthorizedAccessException("Só o senhorio pode aprovar a candidatura.");
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

    private async Task ValidateInitialCoTenantInviteAsync(Property property, Guid tenantId, string? coTenantEmail)
    {
        if (string.IsNullOrWhiteSpace(coTenantEmail)) return;

        var email = coTenantEmail.Trim().ToLowerInvariant();
        if (!email.Contains('@'))
            throw new ArgumentException("Email do co-candidato inválido.");

        var tenant = await _userRepository.GetByIdAsync(tenantId)
            ?? throw new InvalidOperationException("Utilizador não encontrado.");
        if (string.Equals(tenant.Email, email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Não te podes convidar a ti próprio como co-candidato.");

        var landlord = await _userRepository.GetByIdAsync(property.LandlordId);
        if (landlord != null && string.Equals(landlord.Email, email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Não podes convidar o proprietário do imóvel.");

        var invitee = await _userRepository.GetByEmailAsync(email);
        if (invitee == null)
            throw new KeyNotFoundException("user_not_registered");
    }

    private async Task HydrateParticipantProfilesAsync(IEnumerable<ApplicationDto> applications, Guid viewerUserId)
    {
        foreach (var application in applications)
        {
            if (application.CoTenantUserId.HasValue)
            {
                var profile = await _userService.GetPublicProfileAsync(application.CoTenantUserId.Value, viewerUserId);
                application.CoTenantName = profile?.Name;
                application.CoTenantAvatarUrl = profile?.ProfilePictureUrl;
            }

            foreach (var invite in application.CoTenantInvites.Where(i => i.InviteeUserId.HasValue))
            {
                var profile = await _userService.GetPublicProfileAsync(invite.InviteeUserId!.Value, viewerUserId);
                invite.InviteeName = profile?.Name;
                invite.InviteeAvatarUrl = profile?.ProfilePictureUrl;
            }

            foreach (var guarantor in application.Guarantors)
            {
                if (!guarantor.UserId.HasValue)
                {
                    guarantor.UserName = guarantor.GuestName ?? "Fiador";
                    continue;
                }

                var profile = await _userService.GetPublicProfileAsync(guarantor.UserId.Value, viewerUserId);
                guarantor.UserName = profile?.Name ?? guarantor.GuestName ?? "Fiador";
                guarantor.UserAvatarUrl = profile?.ProfilePictureUrl;
            }
        }
    }

    private static void ApplyCurrentUserRole(ApplicationDto application, Guid viewerUserId)
    {
        application.IsCurrentUserCoTenant = application.CoTenantUserId.HasValue && application.CoTenantUserId.Value == viewerUserId;
        application.IsCurrentUserGuarantor = application.Guarantors.Any(g => g.UserId.HasValue && g.UserId.Value == viewerUserId && g.InviteStatus == GuarantorInviteStatus.Accepted.ToString());

        application.CurrentUserRole = application.LandlordId == viewerUserId ? "Landlord"
            : application.TenantId == viewerUserId ? "Tenant"
            : application.IsCurrentUserCoTenant ? "CoTenant"
            : application.IsCurrentUserGuarantor ? "Guarantor"
            : string.Empty;
    }
}

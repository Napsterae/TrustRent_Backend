using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Mappers;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Catalog.Services;

public class GuarantorService : IGuarantorService
{
    private readonly CatalogDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly INotificationService _notificationService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    private const int InviteValidityDays = 7;

    public GuarantorService(
        CatalogDbContext context,
        IUserRepository userRepository,
        IUserService userService,
        INotificationService notificationService,
        IEmailService emailService,
        IConfiguration configuration)
    {
        _context = context;
        _userRepository = userRepository;
        _userService = userService;
        _notificationService = notificationService;
        _emailService = emailService;
        _configuration = configuration;
    }

    // ===== Senhorio =====

    public async Task<ApplicationDto> RequestGuarantorAsync(Guid applicationId, Guid landlordId, RequestGuarantorDto dto)
    {
        var application = await LoadApplicationFullAsync(applicationId);
        var property = application.Property!;

        EnsureLandlord(property, landlordId);
        EnsureGuarantorAllowed(property);

        if (application.GuarantorRequirementStatus == GuarantorRequirementStatus.Approved)
            throw new InvalidOperationException("Já existe fiador aprovado para esta candidatura.");
        if (application.Status is ApplicationStatus.Accepted or ApplicationStatus.ContractPendingSignature or ApplicationStatus.AwaitingPayment or ApplicationStatus.LeaseActive)
            throw new InvalidOperationException("Já não é possível pedir fiador nesta fase da candidatura.");

        application.IsGuarantorRequired = true;
        application.GuarantorRequestedAt = DateTime.UtcNow;
        application.GuarantorRequestNote = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        application.GuarantorRequirementStatus = GuarantorRequirementStatus.Requested;
        application.Status = ApplicationStatus.GuarantorRequested;

        AddHistory(application.Id, landlordId, "GuarantorRequested",
            application.GuarantorRequestNote ?? "Senhorio solicitou fiador.");

        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(
            application.TenantId, "guarantor_requested",
            $"O senhorio solicitou fiador para '{property.Title}'.", application.Id);
        if (application.CoTenantUserId.HasValue)
            await _notificationService.SendNotificationAsync(
                application.CoTenantUserId.Value, "guarantor_requested",
                $"O senhorio solicitou fiador para '{property.Title}'.", application.Id);

        return application.ToDto(property.LandlordId);
    }

    public async Task<ApplicationDto> WaiveGuarantorAsync(Guid applicationId, Guid landlordId, WaiveGuarantorDto dto)
    {
        var application = await LoadApplicationFullAsync(applicationId);
        var property = application.Property!;
        EnsureLandlord(property, landlordId);

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new ArgumentException("É obrigatório indicar a razão da dispensa.");

        application.IsGuarantorRequired = false;
        application.GuarantorRequirementStatus = GuarantorRequirementStatus.Waived;
        application.GuarantorRequestNote = dto.Reason.Trim();

        AddHistory(application.Id, landlordId, "GuarantorWaived", dto.Reason.Trim());
        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(
            application.TenantId, "guarantor_waived",
            "O senhorio dispensou a obrigatoriedade de fiador.", application.Id);

        return application.ToDto(property.LandlordId);
    }

    // ===== Candidato convida =====

    public async Task<GuarantorSummaryDto> InviteGuarantorAsync(Guid applicationId, Guid invitingUserId, CreateGuarantorInviteDto dto, string? sourceIp)
    {
        var application = await LoadApplicationFullAsync(applicationId);
        var property = application.Property!;

        EnsureGuarantorAllowed(property);
        if (!IsTenantSide(application, invitingUserId))
            throw new UnauthorizedAccessException("Só o candidato (ou co-candidato) pode convidar um fiador.");

        var email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Email do fiador inválido.");

        // Não convida tenant/cotenant/landlord
        var inviter = await _userRepository.GetByIdAsync(invitingUserId)
            ?? throw new InvalidOperationException("Utilizador não encontrado.");
        var landlord = await _userRepository.GetByIdAsync(property.LandlordId);
        if (string.Equals(inviter.Email, email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Não te podes convidar a ti próprio como fiador.");
        if (landlord != null && string.Equals(landlord.Email, email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Não podes convidar o senhorio como fiador.");

        if (application.CoTenantUserId.HasValue)
        {
            var coTenant = await _userRepository.GetByIdAsync(application.CoTenantUserId.Value);
            if (coTenant != null && string.Equals(coTenant.Email, email, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Não podes convidar o co-candidato como fiador.");
        }

        // Não pode haver fiador ativo
        if (application.Guarantors.Any(g => g.InviteStatus is GuarantorInviteStatus.Pending or GuarantorInviteStatus.Accepted))
            throw new InvalidOperationException("Já existe um fiador ativo para esta candidatura.");

        var existingUser = await _userRepository.GetByEmailAsync(email);

        var guarantor = new Guarantor
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            UserId = existingUser?.Id,
            GuestEmail = email,
            GuestAccessToken = GenerateGuestToken(),
            GuestTokenIssuedAt = DateTime.UtcNow,
            CreatedFromIp = sourceIp,
            InvitedByUserId = invitingUserId,
            InviteStatus = GuarantorInviteStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(InviteValidityDays)
        };
        _context.Guarantors.Add(guarantor);

        application.GuarantorRequirementStatus = GuarantorRequirementStatus.Requested;
        application.IsGuarantorRequired = true;

        AddHistory(applicationId, invitingUserId, "GuarantorInvited", $"Fiador convidado: {MaskEmail(email)}");
        await _context.SaveChangesAsync();

        if (existingUser != null)
        {
            await _notificationService.SendNotificationAsync(
                existingUser.Id, "guarantor_invite",
                $"{inviter.Name} convidou-te para seres fiador de uma candidatura.", applicationId);
        }

        var guestUrl = BuildGuestUrl(guarantor.GuestAccessToken);
        await _emailService.SendEmailAsync(email,
            "Convite para fiador — TrustRent",
            BuildInviteEmail(inviter.Name, property.Title, guestUrl));

        return await BuildSummaryAsync(guarantor);
    }

    public async Task<GuarantorSummaryDto> GetByGuestTokenAsync(string token)
    {
        var guarantor = await LoadGuarantorByTokenAsync(token);
        guarantor.GuestTokenLastUsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return await BuildSummaryAsync(guarantor);
    }

    public async Task<GuarantorSummaryDto> GetByIdForUserAsync(Guid guarantorId, Guid userId)
    {
        var guarantor = await LoadGuarantorAsync(guarantorId);
        if (!guarantor.UserId.HasValue || guarantor.UserId.Value != userId)
            throw new UnauthorizedAccessException("Este convite de fiador não pertence ao utilizador atual.");

        return await BuildSummaryAsync(guarantor);
    }

    public async Task<GuarantorSummaryDto> AcceptInviteByTokenAsync(string token)
    {
        var guarantor = await LoadGuarantorByTokenAsync(token);
        EnsurePending(guarantor);

        guarantor.InviteStatus = GuarantorInviteStatus.Accepted;
        guarantor.RespondedAt = DateTime.UtcNow;
        guarantor.GuestTokenLastUsedAt = DateTime.UtcNow;

        var app = guarantor.Application!;
        app.GuarantorId = guarantor.Id;
        app.GuarantorRequirementStatus = GuarantorRequirementStatus.Submitted;

        AddHistory(app.Id, app.TenantId, "GuarantorAccepted", "Fiador convidado aceitou o convite por token.");
        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(app.TenantId,
            "guarantor_accepted", "O fiador aceitou o convite.", app.Id);

        return await BuildSummaryAsync(guarantor);
    }

    public async Task<GuarantorSummaryDto> DeclineInviteByTokenAsync(string token, GuarantorDecisionDto dto)
    {
        var guarantor = await LoadGuarantorByTokenAsync(token);
        EnsurePending(guarantor);

        guarantor.InviteStatus = GuarantorInviteStatus.Declined;
        guarantor.RespondedAt = DateTime.UtcNow;
        guarantor.GuestTokenLastUsedAt = DateTime.UtcNow;
        guarantor.DeclineReason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();

        AddHistory(guarantor.ApplicationId, guarantor.Application!.TenantId, "GuarantorDeclined",
            guarantor.DeclineReason ?? "Fiador convidado recusou por token.");
        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(guarantor.Application!.TenantId,
            "guarantor_declined", "O fiador recusou o convite.", guarantor.ApplicationId);

        return await BuildSummaryAsync(guarantor);
    }

    public async Task<GuarantorSummaryDto> AcceptInviteAsync(Guid guarantorId, Guid acceptingUserId)
    {
        var guarantor = await LoadGuarantorAsync(guarantorId);
        EnsureRecipient(guarantor, acceptingUserId);
        EnsurePending(guarantor);

        guarantor.InviteStatus = GuarantorInviteStatus.Accepted;
        guarantor.RespondedAt = DateTime.UtcNow;

        var app = guarantor.Application!;
        app.GuarantorId = guarantor.Id;
        app.GuarantorRequirementStatus = GuarantorRequirementStatus.Submitted;

        AddHistory(app.Id, acceptingUserId, "GuarantorAccepted", "Fiador aceitou o convite.");
        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(app.TenantId,
            "guarantor_accepted", "O fiador aceitou o convite.", app.Id);

        return await BuildSummaryAsync(guarantor);
    }

    public async Task<GuarantorSummaryDto> DeclineInviteAsync(Guid guarantorId, Guid decliningUserId, GuarantorDecisionDto dto)
    {
        var guarantor = await LoadGuarantorAsync(guarantorId);
        EnsureRecipient(guarantor, decliningUserId);
        EnsurePending(guarantor);

        guarantor.InviteStatus = GuarantorInviteStatus.Declined;
        guarantor.RespondedAt = DateTime.UtcNow;
        guarantor.DeclineReason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();

        AddHistory(guarantor.ApplicationId, decliningUserId, "GuarantorDeclined",
            guarantor.DeclineReason ?? "Fiador recusou.");
        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(guarantor.Application!.TenantId,
            "guarantor_declined", "O fiador recusou o convite.", guarantor.ApplicationId);

        return await BuildSummaryAsync(guarantor);
    }

    // ===== Submissão de dados pelo fiador =====

    public async Task<GuarantorSummaryDto> SubmitDataAsync(Guid guarantorId, Guid submittingUserId, SubmitGuarantorDataDto dto)
    {
        var guarantor = await LoadGuarantorAsync(guarantorId);
        if (guarantor.UserId != submittingUserId)
            throw new UnauthorizedAccessException("Só o próprio fiador pode submeter os dados.");

        return await SubmitDataCoreAsync(guarantor, submittingUserId, dto);
    }

    public async Task<GuarantorSummaryDto> SubmitDataByTokenAsync(string token, SubmitGuarantorDataDto dto)
    {
        var guarantor = await LoadGuarantorByTokenAsync(token);
        guarantor.GuestTokenLastUsedAt = DateTime.UtcNow;
        return await SubmitDataCoreAsync(guarantor, guarantor.Application!.TenantId, dto);
    }

    private async Task<GuarantorSummaryDto> SubmitDataCoreAsync(Guarantor guarantor, Guid actorId, SubmitGuarantorDataDto dto)
    {
        if (guarantor.InviteStatus != GuarantorInviteStatus.Accepted)
            throw new InvalidOperationException("Convite tem de ser aceite antes de submeter dados.");

        if (!string.IsNullOrWhiteSpace(dto.FullName)) guarantor.GuestName = dto.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(dto.PhoneNumber)) guarantor.GuestPhoneNumber = dto.PhoneNumber.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Address)) guarantor.GuestAddress = dto.Address.Trim();
        if (!string.IsNullOrWhiteSpace(dto.PostalCode)) guarantor.GuestPostalCode = dto.PostalCode.Trim();

        // KYC simulado (em produção: comparar CC + selfie)
        if (dto.SimulateIdentityMatch)
        {
            guarantor.IsIdentityVerified = true;
            guarantor.IdentityVerifiedAt = DateTime.UtcNow;
            guarantor.IdentityMatchEvidenceHash = Guid.NewGuid().ToString("N");
        }

        if (dto.SimulateAddressMatch && !string.IsNullOrWhiteSpace(guarantor.GuestAddress))
        {
            guarantor.IsAddressVerified = true;
            guarantor.AddressVerifiedAt = DateTime.UtcNow;
        }

        guarantor.EmploymentType = Enum.TryParse<EmploymentType>(dto.EmploymentType, out var et) ? et : EmploymentType.Employee;
        guarantor.IncomeValidationMethod = Enum.TryParse<IncomeValidationMethod>(dto.IncomeValidationMethod, out var im) ? im : IncomeValidationMethod.Payslips;
        guarantor.PayslipsProvidedCount = dto.PayslipsProvidedCount;
        guarantor.EmployerName = dto.EmployerName;
        guarantor.EmployerNif = dto.EmployerNif;
        guarantor.EmploymentStartDate = dto.EmploymentStartDate;

        // Atribuir faixa salarial simulada (primeira ativa)
        var range = await _context.SalaryRanges
            .Where(r => r.IsActive)
            .OrderBy(r => r.DisplayOrder)
            .FirstOrDefaultAsync();
        if (range != null)
        {
            guarantor.IncomeRangeId = range.Id;
            guarantor.IncomeValidatedAt = DateTime.UtcNow;
        }

        var app = guarantor.Application!;
        app.GuarantorRequirementStatus = GuarantorRequirementStatus.LandlordReviewing;
        app.Status = ApplicationStatus.GuarantorReview;

        AddHistory(app.Id, actorId, "GuarantorSubmitted",
            "Fiador submeteu KYC + dados de rendimento.");
        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(app.Property!.LandlordId,
            "guarantor_submitted",
            "O fiador submeteu os dados para a tua revisão.", app.Id);

        return await BuildSummaryAsync(guarantor);
    }

    public async Task<GuarantorSummaryDto> ApproveAsync(Guid guarantorId, Guid landlordId)
    {
        var guarantor = await LoadGuarantorAsync(guarantorId);
        var app = guarantor.Application!;
        EnsureLandlord(app.Property!, landlordId);

        if (guarantor.InviteStatus != GuarantorInviteStatus.Accepted)
            throw new InvalidOperationException("Fiador tem de ter aceitado o convite.");
        if (app.GuarantorRequirementStatus != GuarantorRequirementStatus.LandlordReviewing)
            throw new InvalidOperationException("Fiador ainda não submeteu os dados para revisão.");

        app.GuarantorRequirementStatus = GuarantorRequirementStatus.Approved;
        // Volta à fase de decisão do senhorio para prosseguir o fluxo normal.
        app.Status = ApplicationStatus.InterestConfirmed;

        AddHistory(app.Id, landlordId, "GuarantorApproved", "Fiador aprovado pelo senhorio.");
        await _context.SaveChangesAsync();

        if (guarantor.UserId.HasValue)
        {
            await _notificationService.SendNotificationAsync(guarantor.UserId.Value,
                "guarantor_approved", "Foste aprovado como fiador.", app.Id);
        }
        await _emailService.SendEmailAsync(guarantor.GuestEmail,
            "Fiador aprovado — TrustRent",
            BuildStatusEmail("Fiador aprovado", "O senhorio aprovou os teus dados de fiador. Avisamos-te novamente quando o contrato estiver pronto para assinatura.", BuildGuestUrl(guarantor.GuestAccessToken)));
        await _notificationService.SendNotificationAsync(app.TenantId,
            "guarantor_approved", "O fiador foi aprovado pelo senhorio.", app.Id);

        return await BuildSummaryAsync(guarantor);
    }

    public async Task<GuarantorSummaryDto> RejectAsync(Guid guarantorId, Guid landlordId, GuarantorDecisionDto dto)
    {
        var guarantor = await LoadGuarantorAsync(guarantorId);
        var app = guarantor.Application!;
        EnsureLandlord(app.Property!, landlordId);

        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new ArgumentException("É obrigatório indicar a razão da rejeição.");

        guarantor.RejectionReason = dto.Reason.Trim();
        app.GuarantorRequirementStatus = GuarantorRequirementStatus.Rejected;
        app.Status = ApplicationStatus.InterestConfirmed;
        app.GuarantorId = null; // candidato terá de propor outro

        AddHistory(app.Id, landlordId, "GuarantorRejected", dto.Reason.Trim());
        await _context.SaveChangesAsync();

        if (guarantor.UserId.HasValue)
        {
            await _notificationService.SendNotificationAsync(guarantor.UserId.Value,
                "guarantor_rejected", "Foste rejeitado como fiador.", app.Id);
        }
        await _emailService.SendEmailAsync(guarantor.GuestEmail,
            "Fiador não aprovado — TrustRent",
            BuildStatusEmail("Fiador não aprovado", "O senhorio não aprovou a proposta de fiador para esta candidatura.", BuildGuestUrl(guarantor.GuestAccessToken)));
        await _notificationService.SendNotificationAsync(app.TenantId,
            "guarantor_rejected", "O senhorio rejeitou o fiador proposto.", app.Id);

        return await BuildSummaryAsync(guarantor);
    }

    // ===== Queries =====

    public async Task<IEnumerable<GuarantorSummaryDto>> GetForApplicationAsync(Guid applicationId, Guid requesterId)
    {
        var application = await LoadApplicationFullAsync(applicationId);
        var property = application.Property!;

        var isParticipant = IsTenantSide(application, requesterId)
                            || property.LandlordId == requesterId
                            || application.Guarantors.Any(g => g.UserId.HasValue && g.UserId.Value == requesterId);
        if (!isParticipant)
            throw new UnauthorizedAccessException("Sem permissão para ver fiadores desta candidatura.");

        var list = new List<GuarantorSummaryDto>();
        foreach (var g in application.Guarantors.OrderByDescending(g => g.CreatedAt))
            list.Add(await BuildSummaryAsync(g));
        return list;
    }

    public async Task<IEnumerable<GuarantorSummaryDto>> GetPendingInvitesForUserAsync(Guid userId)
    {
        var invites = await _context.Guarantors
            .Include(g => g.Application).ThenInclude(a => a!.Property)
            .Include(g => g.IncomeRange)
            .Where(g => g.UserId == userId
                        && g.InviteStatus == GuarantorInviteStatus.Pending
                        && g.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        var list = new List<GuarantorSummaryDto>();
        foreach (var g in invites) list.Add(await BuildSummaryAsync(g));
        return list;
    }

    // ===== Helpers =====

    private async Task<Application> LoadApplicationFullAsync(Guid applicationId)
        => await _context.Applications
            .Include(a => a.Property)
            .Include(a => a.Guarantors).ThenInclude(g => g.IncomeRange)
            .FirstOrDefaultAsync(a => a.Id == applicationId)
           ?? throw new KeyNotFoundException("Candidatura não encontrada.");

    private async Task<Guarantor> LoadGuarantorAsync(Guid guarantorId)
        => await _context.Guarantors
            .Include(g => g.Application).ThenInclude(a => a!.Property)
            .Include(g => g.IncomeRange)
            .FirstOrDefaultAsync(g => g.Id == guarantorId)
           ?? throw new KeyNotFoundException("Fiador não encontrado.");

    private async Task<Guarantor> LoadGuarantorByTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedAccessException("Token inválido.");

        return await _context.Guarantors
            .Include(g => g.Application).ThenInclude(a => a!.Property)
            .Include(g => g.IncomeRange)
            .FirstOrDefaultAsync(g => g.GuestAccessToken == token)
           ?? throw new UnauthorizedAccessException("Token inválido.");
    }

    private static void EnsureLandlord(Property property, Guid landlordId)
    {
        if (property.LandlordId != landlordId)
            throw new UnauthorizedAccessException("Só o senhorio do imóvel pode executar esta operação.");
    }

    private static void EnsureGuarantorAllowed(Property property)
    {
        if (!property.HasOfficialContract || !property.AcceptsGuarantor)
            throw new InvalidOperationException("Este imóvel não aceita fiador (requer contrato oficial e configuração explícita).");
    }

    private static void EnsurePending(Guarantor guarantor)
    {
        if (guarantor.InviteStatus != GuarantorInviteStatus.Pending)
            throw new InvalidOperationException("Convite já não está pendente.");
        if (guarantor.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Este convite expirou.");
    }

    private static void EnsureRecipient(Guarantor guarantor, Guid userId)
    {
        if (!guarantor.UserId.HasValue || guarantor.UserId.Value != userId)
            throw new UnauthorizedAccessException("Este convite não é para ti.");
    }

    private static bool IsTenantSide(Application app, Guid userId)
        => app.TenantId == userId || (app.CoTenantUserId.HasValue && app.CoTenantUserId.Value == userId);

    private void AddHistory(Guid applicationId, Guid actorId, string action, string? message)
    {
        _context.ApplicationHistories.Add(new ApplicationHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            ActorId = actorId,
            Action = action,
            Message = message
        });
    }

    private async Task<GuarantorSummaryDto> BuildSummaryAsync(Guarantor guarantor)
    {
        var profile = guarantor.UserId.HasValue
            ? await _userService.GetPublicProfileAsync(guarantor.UserId.Value, guarantor.UserId.Value)
            : null;
        var app = guarantor.Application;
        var property = app?.Property;

        return new GuarantorSummaryDto
        {
            Id = guarantor.Id,
            ApplicationId = guarantor.ApplicationId,
            UserId = guarantor.UserId,
            UserName = guarantor.GuestName ?? profile?.Name ?? "Fiador",
            UserAvatarUrl = profile?.ProfilePictureUrl,
            GuestEmail = guarantor.GuestEmail,
            GuestEmailMasked = MaskEmail(guarantor.GuestEmail),
            GuestName = guarantor.GuestName,
            GuestPhoneNumber = guarantor.GuestPhoneNumber,
            GuestAddress = guarantor.GuestAddress,
            GuestPostalCode = guarantor.GuestPostalCode,
            GuestAccessUrl = BuildGuestUrl(guarantor.GuestAccessToken),
            PropertyTitle = property?.Title,
            PropertyAddress = FormatAddress(property),
            MonthlyRent = property?.Price,
            InviteStatus = guarantor.InviteStatus.ToString(),
            RequirementStatus = app?.GuarantorRequirementStatus.ToString() ?? "NotRequested",
            CreatedAt = guarantor.CreatedAt,
            ExpiresAt = guarantor.ExpiresAt,
            RespondedAt = guarantor.RespondedAt,
            IsIdentityVerified = guarantor.IsIdentityVerified,
            IdentityVerifiedAt = guarantor.IdentityVerifiedAt,
            IsAddressVerified = guarantor.IsAddressVerified,
            AddressVerifiedAt = guarantor.AddressVerifiedAt,
            IncomeRangeCode = guarantor.IncomeRange?.Code,
            IncomeRangeLabel = guarantor.IncomeRange?.Label,
            IncomeValidatedAt = guarantor.IncomeValidatedAt,
            EmploymentType = guarantor.EmploymentType?.ToString(),
            IncomeValidationMethod = guarantor.IncomeValidationMethod?.ToString(),
            PayslipsProvidedCount = guarantor.PayslipsProvidedCount,
            EmployerName = guarantor.EmployerName,
            EmployerNifMasked = MaskNif(guarantor.EmployerNif),
            EmploymentStartDate = guarantor.EmploymentStartDate,
            LandlordRequestNote = guarantor.LandlordRequestNote,
            RejectionReason = guarantor.RejectionReason
        };
    }

    private static string? MaskNif(string? nif)
    {
        if (string.IsNullOrWhiteSpace(nif)) return null;
        return nif.Length <= 3 ? nif : new string('*', nif.Length - 3) + nif[^3..];
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1) return email;
        var local = email[..at];
        return $"{local[..Math.Min(2, local.Length)]}{new string('*', Math.Max(2, local.Length - 2))}{email[at..]}";
    }

    private string BuildGuestUrl(string token)
    {
        var frontendBaseUrl = _configuration["Frontend:BaseUrl"]
            ?? _configuration["App:FrontendBaseUrl"]
            ?? "http://localhost:5173";
        return $"{frontendBaseUrl.TrimEnd('/')}/guarantor/guest/{token}";
    }

    private static string GenerateGuestToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string? FormatAddress(Property? property)
    {
        if (property == null) return null;
        var parts = new[] { property.Street, property.DoorNumber, property.Parish, property.Municipality }
            .Where(p => !string.IsNullOrWhiteSpace(p));
        return string.Join(", ", parts);
    }

    private static string BuildInviteEmail(string inviterName, string propertyTitle, string guestUrl)
        => BuildEmailShell(
            "Convite para seres fiador",
            $"{inviterName} indicou-te como fiador para a candidatura ao imóvel <strong>{propertyTitle}</strong>.",
            "Acede de forma segura, confirma os dados da candidatura e submete a tua informação e documentos.",
            "Abrir convite",
            guestUrl);

    private static string BuildStatusEmail(string title, string message, string guestUrl)
        => BuildEmailShell(title, message, "Podes consultar o estado atualizado através da tua área segura de convidado.", "Consultar candidatura", guestUrl);

    private static string BuildEmailShell(string title, string lead, string body, string buttonText, string url)
        => $"""
           <div style="margin:0;padding:32px;background:#f3f4f6;font-family:Inter,Segoe UI,Arial,sans-serif;color:#111827">
             <div style="max-width:640px;margin:0 auto;background:#ffffff;border:1px solid #e5e7eb;border-radius:18px;overflow:hidden">
               <div style="padding:28px 32px;background:#0f766e;color:#ffffff">
                 <div style="font-size:13px;font-weight:700;letter-spacing:.08em;text-transform:uppercase;opacity:.85">TrustRent</div>
                 <h1 style="margin:10px 0 0;font-size:26px;line-height:1.2">{title}</h1>
               </div>
               <div style="padding:32px">
                 <p style="font-size:16px;line-height:1.6;margin:0 0 16px">{lead}</p>
                 <p style="font-size:15px;line-height:1.6;color:#4b5563;margin:0 0 28px">{body}</p>
                 <a href="{url}" style="display:inline-block;background:#0f766e;color:#ffffff;text-decoration:none;font-weight:700;padding:13px 20px;border-radius:12px">{buttonText}</a>
                 <p style="font-size:12px;line-height:1.5;color:#6b7280;margin:28px 0 0">Se o botão não funcionar, copia este endereço: <br><span style="word-break:break-all;color:#374151">{url}</span></p>
               </div>
             </div>
           </div>
           """;
}

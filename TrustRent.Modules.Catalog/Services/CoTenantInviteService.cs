using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Communications;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Catalog.Services;

public class CoTenantInviteService : ICoTenantInviteService
{
    private readonly CatalogDbContext _context;
    private readonly IUserRepository _userRepository;
    private readonly IUserService _userService;
    private readonly INotificationService _notificationService;
    private readonly ICommunicationContentService _communicationContentService;
    private readonly IEmailService _emailService;
    private readonly ILogger<CoTenantInviteService>? _logger;

    private const int InviteValidityDays = 7;

    public CoTenantInviteService(
        CatalogDbContext context,
        IUserRepository userRepository,
        IUserService userService,
        INotificationService notificationService,
        ICommunicationContentService communicationContentService,
        IEmailService emailService,
        ILogger<CoTenantInviteService>? logger = null)
    {
        _context = context;
        _userRepository = userRepository;
        _userService = userService;
        _notificationService = notificationService;
        _communicationContentService = communicationContentService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<CoTenantInviteDto> CreateInviteAsync(Guid applicationId, Guid inviterUserId, CreateCoTenantInviteDto dto, string? sourceIp)
    {
        var application = await _context.Applications
            .Include(a => a.Property).ThenInclude(p => p!.Images)
            .Include(a => a.CoTenantInvites)
            .FirstOrDefaultAsync(a => a.Id == applicationId)
            ?? throw new KeyNotFoundException("Candidatura não encontrada.");

        if (application.TenantId != inviterUserId)
            throw new UnauthorizedAccessException("Só o candidato principal pode convidar um co-candidato.");

        var email = (dto.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Email do co-candidato inválido.");

        // Não pode convidar a si próprio
        var inviter = await _userRepository.GetByIdAsync(inviterUserId)
            ?? throw new InvalidOperationException("Utilizador não encontrado.");
        if (string.Equals(inviter.Email, email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Não te podes convidar a ti próprio como co-candidato.");

        // Não pode convidar o senhorio
        var landlord = await _userRepository.GetByIdAsync(application.Property!.LandlordId);
        if (landlord != null && string.Equals(landlord.Email, email, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Não podes convidar o proprietário do imóvel.");

        var invitee = await _userRepository.GetByEmailAsync(email);

        var activeInvite = application.CoTenantInvites
            .FirstOrDefault(i => i.Status is CoTenantInviteStatus.Pending or CoTenantInviteStatus.Accepted);
        if (activeInvite != null)
        {
            if (activeInvite.Status == CoTenantInviteStatus.Pending
                && string.Equals(activeInvite.InviteeEmail, email, StringComparison.OrdinalIgnoreCase))
            {
                return await BuildDtoAsync(activeInvite, application.Property);
            }

            throw new InvalidOperationException("Já existe um convite ativo para esta candidatura.");
        }

        var invite = new ApplicationCoTenantInvite
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            Application = application,
            InviterUserId = inviterUserId,
            InviteeEmail = email,
            InviteeUserId = invitee?.Id,
            Status = CoTenantInviteStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(InviteValidityDays),
            CreatedFromIp = sourceIp
        };

        _context.ApplicationCoTenantInvites.Add(invite);

        // Histórico
        _context.ApplicationHistories.Add(new ApplicationHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            ActorId = inviterUserId,
            Action = "CoTenantInvited",
            Message = $"Convite enviado para {MaskEmail(email)}.",
            EventData = null
        });

        await _context.SaveChangesAsync();

        // Notificações e email são best-effort; a candidatura não deve falhar depois de persistida.
        if (invitee != null)
        {
            try
            {
                await _notificationService.SendNotificationAsync(
                    invitee.Id,
                    "cotenant_invite",
                    $"{inviter.Name} convidou-te para co-candidatar a '{application.Property.Title}'.",
                    applicationId);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Falha ao enviar a notificação do convite de co-candidato {InviteId} para o utilizador {InviteeUserId}.", invite.Id, invitee.Id);
            }
        }

        try
        {
            var renderedTemplate = await _communicationContentService.RenderEmailTemplateAsync(
                CommunicationEmailTemplateKeys.ApplicationCoTenantInvite,
                new Dictionary<string, string?>
                {
                    ["InviterName"] = inviter.Name,
                    ["PropertyTitle"] = application.Property.Title
                });

            await _emailService.SendEmailAsync(email, renderedTemplate.Subject, renderedTemplate.BodyHtml);
        }
        catch (Exception ex)
            {
            _logger?.LogWarning(ex, "Falha ao enviar o email do convite de co-candidato {InviteId} para {InviteeEmail}.", invite.Id, email);
        }

        return await BuildDtoAsync(invite, application.Property);
    }

    public async Task<CoTenantInviteDto> AcceptInviteAsync(Guid inviteId, Guid acceptingUserId)
    {
        var invite = await _context.ApplicationCoTenantInvites
            .Include(i => i.Application).ThenInclude(a => a!.Property).ThenInclude(p => p!.Images)
            .Include(i => i.Application).ThenInclude(a => a!.CoTenantInvites)
            .FirstOrDefaultAsync(i => i.Id == inviteId)
            ?? throw new KeyNotFoundException("Convite não encontrado.");

        ValidateRecipient(invite, acceptingUserId);
        EnsurePending(invite);

        var app = invite.Application!;
        if (app.TenantId == acceptingUserId)
            throw new InvalidOperationException("Já és o candidato principal desta candidatura.");
        if (app.CoTenantUserId.HasValue)
            throw new InvalidOperationException("Esta candidatura já tem co-candidato.");

        invite.Status = CoTenantInviteStatus.Accepted;
        invite.RespondedAt = DateTime.UtcNow;
        if (!invite.InviteeUserId.HasValue) invite.InviteeUserId = acceptingUserId;

        app.CoTenantUserId = acceptingUserId;
        app.CoTenantJoinedAt = DateTime.UtcNow;

        _context.ApplicationHistories.Add(new ApplicationHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = app.Id,
            ActorId = acceptingUserId,
            Action = "CoTenantAccepted",
            Message = "Convite de co-candidato aceite."
        });

        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(
            invite.InviterUserId,
            "cotenant_accepted",
            $"O teu co-candidato aceitou o convite para '{app.Property!.Title}'.",
            app.Id);

        return await BuildDtoAsync(invite, app.Property);
    }

    public async Task<CoTenantInviteDto> DeclineInviteAsync(Guid inviteId, Guid decliningUserId, RespondCoTenantInviteDto dto)
    {
        var invite = await _context.ApplicationCoTenantInvites
            .Include(i => i.Application).ThenInclude(a => a!.Property).ThenInclude(p => p!.Images)
            .FirstOrDefaultAsync(i => i.Id == inviteId)
            ?? throw new KeyNotFoundException("Convite não encontrado.");

        ValidateRecipient(invite, decliningUserId);
        EnsurePending(invite);

        invite.Status = CoTenantInviteStatus.Declined;
        invite.RespondedAt = DateTime.UtcNow;
        invite.DeclineReason = string.IsNullOrWhiteSpace(dto.Reason) ? null : dto.Reason.Trim();
        if (!invite.InviteeUserId.HasValue) invite.InviteeUserId = decliningUserId;

        _context.ApplicationHistories.Add(new ApplicationHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = invite.ApplicationId,
            ActorId = decliningUserId,
            Action = "CoTenantDeclined",
            Message = invite.DeclineReason ?? "Convite recusado."
        });

        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(
            invite.InviterUserId,
            "cotenant_declined",
            $"O convite de co-candidato foi recusado.",
            invite.ApplicationId);

        return await BuildDtoAsync(invite, invite.Application!.Property);
    }

    public async Task<CoTenantInviteDto> CancelInviteAsync(Guid inviteId, Guid cancellingUserId)
    {
        var invite = await _context.ApplicationCoTenantInvites
            .Include(i => i.Application).ThenInclude(a => a!.Property).ThenInclude(p => p!.Images)
            .FirstOrDefaultAsync(i => i.Id == inviteId)
            ?? throw new KeyNotFoundException("Convite não encontrado.");

        if (invite.InviterUserId != cancellingUserId)
            throw new UnauthorizedAccessException("Só o candidato principal pode cancelar este convite.");
        EnsurePending(invite);

        invite.Status = CoTenantInviteStatus.Cancelled;
        invite.RespondedAt = DateTime.UtcNow;

        _context.ApplicationHistories.Add(new ApplicationHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = invite.ApplicationId,
            ActorId = cancellingUserId,
            Action = "CoTenantInviteCancelled",
            Message = "Convite cancelado pelo candidato principal."
        });

        await _context.SaveChangesAsync();
        return await BuildDtoAsync(invite, invite.Application!.Property);
    }

    public async Task<IEnumerable<CoTenantInviteDto>> GetPendingInvitesForUserAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        var emailLower = user?.Email?.ToLowerInvariant();

        var query = _context.ApplicationCoTenantInvites
            .Include(i => i.Application).ThenInclude(a => a!.Property).ThenInclude(p => p!.Images)
            .Where(i => i.Status == CoTenantInviteStatus.Pending && i.ExpiresAt > DateTime.UtcNow)
            .Where(i => i.InviteeUserId == userId || (emailLower != null && i.InviteeEmail == emailLower));

        var invites = await query.ToListAsync();
        var result = new List<CoTenantInviteDto>(invites.Count);
        foreach (var inv in invites)
            result.Add(await BuildDtoAsync(inv, inv.Application!.Property));
        return result;
    }

    public async Task<IEnumerable<CoTenantInviteDto>> GetInvitesForApplicationAsync(Guid applicationId, Guid requesterId)
    {
        var application = await _context.Applications
            .Include(a => a.Property).ThenInclude(p => p!.Images)
            .Include(a => a.CoTenantInvites)
            .FirstOrDefaultAsync(a => a.Id == applicationId)
            ?? throw new KeyNotFoundException("Candidatura não encontrada.");

        var isParticipant = application.TenantId == requesterId
                            || application.CoTenantUserId == requesterId
                            || application.Property!.LandlordId == requesterId
                            || application.CoTenantInvites.Any(i => i.InviteeUserId == requesterId && i.Status == CoTenantInviteStatus.Pending);

        if (!isParticipant)
            throw new UnauthorizedAccessException("Sem permissão para ver convites desta candidatura.");

        var result = new List<CoTenantInviteDto>(application.CoTenantInvites.Count);
        foreach (var inv in application.CoTenantInvites.OrderByDescending(i => i.CreatedAt))
            result.Add(await BuildDtoAsync(inv, application.Property));
        return result;
    }

    private static void EnsurePending(ApplicationCoTenantInvite invite)
    {
        if (invite.Status != CoTenantInviteStatus.Pending)
            throw new InvalidOperationException("Convite já não está pendente.");
        if (invite.ExpiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Este convite expirou.");
    }

    private static void ValidateRecipient(ApplicationCoTenantInvite invite, Guid userId)
    {
        if (invite.InviteeUserId.HasValue && invite.InviteeUserId.Value != userId)
            throw new UnauthorizedAccessException("Este convite não é para ti.");
    }

    private async Task<CoTenantInviteDto> BuildDtoAsync(ApplicationCoTenantInvite invite, Property? property)
    {
        var inviterProfile = await _userService.GetPublicProfileAsync(invite.InviterUserId, invite.InviterUserId);

        string? inviteeName = null;
        string? inviteeAvatar = null;
        if (invite.InviteeUserId.HasValue)
        {
            var profile = await _userService.GetPublicProfileAsync(invite.InviteeUserId.Value, invite.InviterUserId);
            inviteeName = profile?.Name;
            inviteeAvatar = profile?.ProfilePictureUrl;
        }

        return new CoTenantInviteDto
        {
            Id = invite.Id,
            ApplicationId = invite.ApplicationId,
            InviterUserId = invite.InviterUserId,
            InviterName = inviterProfile?.Name ?? "Candidato",
            InviterAvatarUrl = inviterProfile?.ProfilePictureUrl,
            InviterTrustScore = inviterProfile?.TrustScore ?? 0,
            InviterIdentityVerified = inviterProfile?.IsIdentityVerified ?? false,
            InviterNoDebtVerified = inviterProfile?.IsNoDebtVerified ?? false,
            InviteeEmail = invite.InviteeEmail,
            InviteeEmailMasked = MaskEmail(invite.InviteeEmail),
            InviteeUserId = invite.InviteeUserId,
            InviteeName = inviteeName,
            InviteeAvatarUrl = inviteeAvatar,
            Status = invite.Status.ToString(),
            CreatedAt = invite.CreatedAt,
            ExpiresAt = invite.ExpiresAt,
            RespondedAt = invite.RespondedAt,
            DeclineReason = invite.DeclineReason,
            PropertyId = property?.Id,
            PropertyTitle = property?.Title,
            PropertyImageUrl = property?.Images?.FirstOrDefault(i => i.IsMain)?.Url ?? property?.Images?.FirstOrDefault()?.Url,
            PropertyAddress = FormatAddress(property),
            MonthlyRent = property?.Price,
            DurationMonths = invite.Application?.DurationMonths ?? 0,
            ApplicationMessage = invite.Application?.Message ?? string.Empty,
            ApplicationStatus = invite.Application?.Status.ToString() ?? string.Empty
        };
    }

    private static string? FormatAddress(Property? property)
    {
        if (property is null) return null;

        var parts = new[] { property.Street, property.DoorNumber, property.Parish, property.Municipality }
            .Where(p => !string.IsNullOrWhiteSpace(p));

        return string.Join(", ", parts);
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

    private static string BuildInviteEmail(string inviterName, string propertyTitle)
        => $"""
           <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155">{WebUtility.HtmlEncode(inviterName)} convidou-te para co-candidatares ao imóvel <strong>{WebUtility.HtmlEncode(propertyTitle)}</strong>.</p>
           <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 18px;mso-table-lspace:0pt;mso-table-rspace:0pt">
               <tr>
                   <td style="padding:18px 20px;background-color:#f7f3ee;background-image:linear-gradient(90deg,#fff7ef 0%,#f3fbf9 100%);border:1px solid #e5d6c6;border-radius:20px">
                       <p style="margin:0 0 8px;font-size:12px;line-height:1.4;letter-spacing:1.6px;text-transform:uppercase;font-weight:700;color:#1e6b66">Como entrar</p>
                       <p style="margin:0;font-size:14px;line-height:1.7;color:#475569">Entra na Wekaza com este mesmo email para veres o convite pendente no teu painel.</p>
                   </td>
               </tr>
           </table>
           <p style="margin:0;font-size:14px;line-height:1.6;color:#64748b">Se ainda não tens conta, a Wekaza cria-a automaticamente quando validares o teu código de acesso. Não precisas de criar password.</p>
           """;
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Communications.Contracts.Database;
using TrustRent.Modules.Communications.Models;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Communications;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Communications.Hubs;

[Authorize]
public class ApplicationChatHub : Hub
{
    private readonly CommunicationsDbContext _context;
    private readonly IApplicationStatusValidator _statusValidator;
    private readonly INotificationService _notificationService;
    private readonly IUserService? _userService;
    private readonly ICommunicationContentService? _communicationContentService;
    private readonly IEmailService? _emailService;
    private readonly IConfiguration? _configuration;
    private readonly ILogger<ApplicationChatHub>? _logger;

    public ApplicationChatHub(
        CommunicationsDbContext context,
        IApplicationStatusValidator statusValidator,
        INotificationService notificationService,
        IUserService? userService = null,
        ICommunicationContentService? communicationContentService = null,
        IEmailService? emailService = null,
        IConfiguration? configuration = null,
        ILogger<ApplicationChatHub>? logger = null)
    {
        _context = context;
        _statusValidator = statusValidator;
        _notificationService = notificationService;
        _userService = userService;
        _communicationContentService = communicationContentService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    private Guid GetAuthenticatedUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier) ?? Context.User?.FindFirst("sub");
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
            throw new HubException("Utilizador não autenticado.");
        return userId;
    }

    /// <summary>
    /// Join a room specific to an Application ID — only participants can join
    /// </summary>
    public async Task JoinApplicationGroup(Guid applicationId)
    {
        var userId = GetAuthenticatedUserId();
        
        // Verify the user is a participant of this application
        var participants = await _statusValidator.GetApplicationParticipantsAsync(applicationId);
        if (participants == null)
            throw new HubException("Candidatura não encontrada.");
        if (!IsApplicationChatParticipant(participants.Value, userId))
            throw new HubException("Não tem permissão para aceder a esta conversa.");
        
        await Groups.AddToGroupAsync(Context.ConnectionId, applicationId.ToString());
    }

    /// <summary>
    /// Send a message within an application group — sender must be authenticated user
    /// </summary>
    public async Task SendMessage(Guid applicationId, Guid senderId, string content)
    {
        var userId = GetAuthenticatedUserId();
        
        // Prevent impersonation: senderId MUST match the authenticated user
        if (senderId != userId)
            throw new HubException("Não pode enviar mensagens em nome de outro utilizador.");

        // Verify the user is a participant
        var chatContext = await _statusValidator.GetApplicationChatContextAsync(applicationId);
        if (chatContext == null)
            throw new HubException("Candidatura não encontrada.");
        if (!IsApplicationChatParticipant((chatContext.TenantId, chatContext.LandlordId, chatContext.CoTenantUserId), userId))
            throw new HubException("Não tem permissão para enviar mensagens nesta conversa.");

        bool isLocked = await _statusValidator.IsApplicationChatLockedAsync(applicationId);
        if (isLocked)
        {
            throw new HubException("This conversation is strictly locked as the Application has already been accepted or rejected.");
        }

        // 1. Persistir na base de dados (Comunicações)
        var message = new Message
        {
            ContextId = applicationId,
            ContextType = MessageContextType.Application,
            SenderId = senderId,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };
        
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // 2. Emitir a todos os utilizadores ligados no quarto atual
        await Clients.Group(applicationId.ToString()).SendAsync("ReceiveMessage", message);

        // 3. Notificar o outro participante (SignalR + Persistência)
        var recipientIds = GetApplicationChatRecipients((chatContext.TenantId, chatContext.LandlordId, chatContext.CoTenantUserId))
            .Where(recipientId => recipientId != senderId);

        foreach (var recipientId in recipientIds)
        {
            try
            {
                await _notificationService.SendNotificationAsync(recipientId, "application", "Recebeste uma nova mensagem na candidatura.", applicationId);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(
                    ex,
                    "Falha ao enviar notificacao de chat da candidatura {ApplicationId} para o utilizador {RecipientId}.",
                    applicationId,
                    recipientId);
            }

            await TrySendNewMessageEmailAsync(recipientId, senderId, applicationId, chatContext.PropertyTitle, content);
        }
    }

    private async Task TrySendNewMessageEmailAsync(Guid recipientId, Guid senderId, Guid applicationId, string propertyTitle, string content)
    {
        if (_userService is null || _communicationContentService is null || _emailService is null)
            return;

        try
        {
            var recipient = await _userService.GetProfileAsync(recipientId);
            var sender = await _userService.GetProfileAsync(senderId);
            if (recipient is null || sender is null || string.IsNullOrWhiteSpace(recipient.Email))
                return;

            var renderedTemplate = await _communicationContentService.RenderEmailTemplateAsync(
                CommunicationEmailTemplateKeys.ApplicationNewMessage,
                new Dictionary<string, string?>
                {
                    ["SenderName"] = sender.Name,
                    ["PropertyTitle"] = propertyTitle,
                    ["MessagePreview"] = BuildMessagePreview(content),
                    ["ApplicationUrl"] = BuildFrontendUrl($"/applications/{applicationId}")
                });

            await _emailService.SendEmailAsync(recipient.Email, renderedTemplate.Subject, renderedTemplate.BodyHtml);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "Falha ao enviar email de nova mensagem da candidatura {ApplicationId} para o utilizador {RecipientId}.",
                applicationId,
                recipientId);
        }
    }

    private string BuildFrontendUrl(string relativePath)
    {
        var frontendBaseUrl = _configuration?["Frontend:BaseUrl"]
            ?? _configuration?["App:FrontendBaseUrl"]
            ?? "http://localhost:5173";

        return $"{frontendBaseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }

    private static string BuildMessagePreview(string content)
    {
        var normalized = string.Join(' ', (content ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (normalized.Length <= 180)
            return normalized;

        return $"{normalized[..177]}...";
    }

    private static bool IsApplicationChatParticipant((Guid TenantId, Guid LandlordId, Guid? CoTenantUserId) participants, Guid userId)
        => participants.TenantId == userId
           || participants.LandlordId == userId
           || participants.CoTenantUserId == userId;

    private static IEnumerable<Guid> GetApplicationChatRecipients((Guid TenantId, Guid LandlordId, Guid? CoTenantUserId) participants)
    {
        yield return participants.TenantId;
        yield return participants.LandlordId;

        if (participants.CoTenantUserId.HasValue)
            yield return participants.CoTenantUserId.Value;
    }
}

using Microsoft.AspNetCore.SignalR;
using TrustRent.Modules.Communications.Contracts.Database;
using TrustRent.Modules.Communications.Models;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Communications.Hubs;

public class ApplicationChatHub : Hub
{
    private readonly CommunicationsDbContext _context;
    private readonly IApplicationStatusValidator _statusValidator;

    public ApplicationChatHub(CommunicationsDbContext context, IApplicationStatusValidator statusValidator)
    {
        _context = context;
        _statusValidator = statusValidator;
    }

    /// <summary>
    /// Join a room specific to an Application ID
    /// </summary>
    public async Task JoinApplicationGroup(Guid applicationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, applicationId.ToString());
    }

    /// <summary>
    /// Send a message within an application group
    /// </summary>
    public async Task SendMessage(Guid applicationId, Guid senderId, string content)
    {
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
    }
}

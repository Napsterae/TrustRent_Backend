using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace TrustRent.Modules.Communications.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = GetAuthenticatedUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetAuthenticatedUserId();
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
        await base.OnDisconnectedAsync(exception);
    }

    private string? GetAuthenticatedUserId()
        => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;
}

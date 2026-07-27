// SignalR hub for clients that are not Blazor circuits (mobile, external apps).
// Blazor pages get their updates through NotificationBroadcaster instead.
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Joka.Hubs;

public class NotificationHub : Hub
{
    public static string GroupFor(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        // Group per user so a notification never leaks to another account.
        var id = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(id, out var userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId));

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var id = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (Guid.TryParse(id, out var userId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(userId));

        await base.OnDisconnectedAsync(exception);
    }
}

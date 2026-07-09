using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FuelStation.Hubs;

/// <summary>
/// SignalR Hub for real-time push notifications.
/// Clients connect to receive notifications pushed from the server.
/// Supports user-specific notifications (via groups) and broadcast to all.
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    /// <summary>
    /// Called when a client connects. Registers them into groups based on their role.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            // Add to user-specific group for targeted notifications
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
        }

        // Admins join the admin group for low-stock alerts, etc.
        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        }

        // All authenticated users join customers group for promo broadcasts
        await Groups.AddToGroupAsync(Context.ConnectionId, "customers");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
        }

        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admins");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "customers");

        await base.OnDisconnectedAsync(exception);
    }
}

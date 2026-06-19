using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace Comblang.Hubs;

/// <summary>
/// SignalR hub for real-time notifications (new messages, likes, matches, etc.).
/// Requires authentication — each user joins a notification-scoped group on connect.
/// </summary>
public class NotificationHub : Hub
{
    /// <summary>
    /// Called when a client connects. Extracts the user ID from claims and
    /// adds the connection to a user-specific notification group.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        Guid? userId = GetUserIdFromContext();
        if (userId.HasValue)
        {
            string groupName = $"notification_{userId.Value}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Notifies a specific user about a new message.
    /// The message is delivered to all connections in the user's notification group.
    /// </summary>
    public async Task NewMessage(Guid userId, object messageData)
    {
        string groupName = $"notification_{userId}";
        await Clients.Group(groupName).SendAsync("NewMessage", messageData);
    }

    /// <summary>
    /// Sends a notification to a specific user with type, title, and message.
    /// </summary>
    public async Task SendNotification(string userId, string type, string title, string message)
    {
        await Clients.User(userId).SendAsync("ReceiveNotification", new
        {
            type,
            title,
            message,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Notifies a user about a new match.
    /// </summary>
    public async Task NotifyMatch(string userId, string matchedUsername, double compatibilityScore)
    {
        await Clients.User(userId).SendAsync("NewMatch", new
        {
            username = matchedUsername,
            score = compatibilityScore,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Notifies about a new like received.
    /// </summary>
    public async Task NotifyLike(string userId, string likerUsername)
    {
        await Clients.User(userId).SendAsync("NewLike", new
        {
            username = likerUsername,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Extracts the authenticated user's Guid from claims.
    /// </summary>
    private Guid? GetUserIdFromContext()
    {
        Claim? userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirst("sub")
            ?? Context.User?.FindFirst("userId");

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out Guid userId))
            return userId;

        return null;
    }
}

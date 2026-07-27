// Real-time notifications.
//
// Two channels on purpose:
//   - a singleton in-process event, which is what actually updates open Blazor
//     circuits (Blazor Server already pushes over its own SignalR connection,
//     so a second client-side connection would buy nothing)
//   - NotificationHub, mapped for external/mobile clients that are not circuits
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Joka.Data;
using Joka.Hubs;
using Joka.Models.Users;

namespace Joka.Services;

/// <summary>Singleton. Fan-out point every circuit subscribes to.</summary>
public class NotificationBroadcaster
{
    /// <summary>Raised after a notification is persisted. Handlers must not throw.</summary>
    public event Func<Guid, UserNotification, Task>? Notified;

    public async Task PublishAsync(Guid userId, UserNotification notification)
    {
        if (Notified is null) return;

        foreach (var handler in Notified.GetInvocationList().Cast<Func<Guid, UserNotification, Task>>())
        {
            try { await handler(userId, notification); }
            catch { /* a dead circuit must not break the others */ }
        }
    }
}

public class NotificationService
{
    private readonly AppDbContext _db;
    private readonly NotificationBroadcaster _broadcaster;
    private readonly IHubContext<NotificationHub> _hub;

    public NotificationService(
        AppDbContext db,
        NotificationBroadcaster broadcaster,
        IHubContext<NotificationHub> hub)
    {
        _db = db;
        _broadcaster = broadcaster;
        _hub = hub;
    }

    public async Task<UserNotification> SendAsync(
        Guid userId, string title, string message, string type = "System", string? actionUrl = null)
    {
        var notification = new UserNotification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            ActionUrl = actionUrl,
            SentAt = DateTime.UtcNow
        };

        _db.UserNotifications.Add(notification);
        await _db.SaveChangesAsync();

        // Open Blazor circuits first, then any external subscriber.
        await _broadcaster.PublishAsync(userId, notification);

        try
        {
            await _hub.Clients.Group(NotificationHub.GroupFor(userId))
                .SendAsync("notification", new { notification.Title, notification.Message, notification.Type });
        }
        catch { /* the hub is a convenience channel, never the source of truth */ }

        return notification;
    }

    public Task<int> UnreadCountAsync(Guid userId) =>
        _db.UserNotifications.CountAsync(n => n.UserId == userId && !n.IsRead);
}

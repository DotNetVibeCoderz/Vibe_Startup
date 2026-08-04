using FastRide.Data;
using FastRide.Shared.Models;

namespace FastRide.Api.Services;

/// <summary>
/// Writes in-app notifications. Rows are queued onto the current unit of work and persisted by
/// the caller's SaveChanges, so a notification never survives an order that failed to save.
/// </summary>
public sealed class NotificationService(FastRideDbContext db)
{
    public Task QueueAsync(Guid userId, string title, string message, NotificationType type, Guid? orderId = null)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            OrderId = orderId,
            CreatedAt = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }

    /// <summary>Queue and persist immediately, for callers that are not already saving.</summary>
    public async Task SendAsync(Guid userId, string title, string message, NotificationType type, Guid? orderId = null, CancellationToken ct = default)
    {
        await QueueAsync(userId, title, message, type, orderId);
        await db.SaveChangesAsync(ct);
    }
}

using FuelStation.Data;
using FuelStation.Hubs;
using FuelStation.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FuelStation.Services;

/// <summary>
/// Service for managing notifications: creation, retrieval, marking as read,
/// and real-time push via SignalR.
/// </summary>
public class NotificationService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IDbContextFactory<AppDbContext> dbFactory,
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _dbFactory = dbFactory;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Sends a notification to a specific customer and stores it in the database.
    /// Pushes the notification in real-time via SignalR if the customer is online.
    /// </summary>
    public async Task<Notification> SendNotificationAsync(
        string title,
        string message,
        string type = "System",
        Guid? customerId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Title = title,
            Message = message,
            Type = type,
            CustomerId = customerId,
            IsRead = false,
            SentAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        // Push via SignalR to the specific user's group
        if (customerId.HasValue)
        {
            await _hubContext.Clients
                .Group($"user-{customerId}")
                .SendAsync("ReceiveNotification", notification);
        }

        _logger.LogInformation(
            "Notification sent: {Title} | Type: {Type} | CustomerId: {CustomerId}",
            title, type, customerId);

        return notification;
    }

    /// <summary>
    /// Broadcasts a promotional notification to all customers (authenticated users).
    /// </summary>
    public async Task SendPromoNotificationAsync(string title, string message)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Title = title,
            Message = message,
            Type = "Promo",
            CustomerId = null, // null = broadcast to all
            IsRead = false,
            SentAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        // Broadcast to all customers via the "customers" group
        await _hubContext.Clients
            .Group("customers")
            .SendAsync("ReceiveNotification", notification);

        _logger.LogInformation("Promo broadcast sent: {Title}", title);
    }

    /// <summary>
    /// Sends a low-stock alert to all admin users.
    /// </summary>
    public async Task SendLowStockAlertAsync(string tankName, decimal remaining)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Title = "⚠️ Low Stock Alert",
            Message = $"Tank '{tankName}' is running low! Remaining: {remaining:N0} liters. Please schedule a refill.",
            Type = "Alert",
            CustomerId = null,
            IsRead = false,
            SentAt = DateTime.UtcNow,
            ActionUrl = "/iot",
            CreatedAt = DateTime.UtcNow
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync();

        // Push to admin group
        await _hubContext.Clients
            .Group("admins")
            .SendAsync("ReceiveNotification", notification);

        _logger.LogWarning(
            "Low stock alert: Tank={TankName}, Remaining={Remaining}L",
            tankName, remaining);
    }

    /// <summary>
    /// Retrieves all unread notifications for a given user.
    /// Broadcast notifications (CustomerId == null) are included for all users.
    /// </summary>
    public async Task<List<Notification>> GetUnreadNotificationsAsync(Guid userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Notifications
            .Where(n => !n.IsRead
                && (n.CustomerId == null || n.CustomerId == userId))
            .OrderByDescending(n => n.SentAt)
            .Take(50)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves all notifications (read + unread) for a given user with pagination.
    /// </summary>
    public async Task<(List<Notification> Items, int TotalCount)> GetNotificationsAsync(
        Guid userId,
        string? typeFilter = null,
        int page = 1,
        int pageSize = 20)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var query = db.Notifications
            .Where(n => n.CustomerId == null || n.CustomerId == userId)
            .AsQueryable();

        if (!string.IsNullOrEmpty(typeFilter) && typeFilter != "All")
        {
            query = query.Where(n => n.Type == typeFilter);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(n => n.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <summary>
    /// Gets the total count of unread notifications for a user (for badge display).
    /// </summary>
    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.Notifications
            .CountAsync(n => !n.IsRead
                && (n.CustomerId == null || n.CustomerId == userId));
    }

    /// <summary>
    /// Marks a single notification as read.
    /// </summary>
    public async Task MarkAsReadAsync(Guid notificationId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var notification = await db.Notifications.FindAsync(notificationId);
        if (notification != null && !notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            _logger.LogDebug("Notification {Id} marked as read", notificationId);
        }
    }

    /// <summary>
    /// Marks all notifications for a user as read.
    /// </summary>
    public async Task MarkAllAsReadAsync(Guid userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var unreadNotifications = await db.Notifications
            .Where(n => !n.IsRead
                && (n.CustomerId == null || n.CustomerId == userId))
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
            notification.UpdatedAt = now;
        }

        await db.SaveChangesAsync();

        _logger.LogInformation(
            "Marked {Count} notifications as read for UserId={UserId}",
            unreadNotifications.Count, userId);
    }

    /// <summary>
    /// Deletes old notifications (older than the specified number of days).
    /// Useful for cleanup jobs.
    /// </summary>
    public async Task<int> CleanupOldNotificationsAsync(int olderThanDays = 90)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
        var oldNotifications = await db.Notifications
            .Where(n => n.IsRead && n.CreatedAt < cutoff)
            .ToListAsync();

        db.Notifications.RemoveRange(oldNotifications);
        var count = await db.SaveChangesAsync();

        _logger.LogInformation("Cleaned up {Count} old notifications", count);
        return count;
    }
}

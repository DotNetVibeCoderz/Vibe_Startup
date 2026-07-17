using WashUp.Data;
using WashUp.Models;
using Microsoft.EntityFrameworkCore;

namespace WashUp.Services;

/// <summary>
/// Service for managing notifications across the application
/// </summary>
public class NotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Send notification to a specific user
    /// </summary>
    public async Task SendAsync(string? userId, string type, string title, string? message, string? actionUrl = null, string priority = "Normal")
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ActionUrl = actionUrl,
            Priority = priority,
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Send order status update notification
    /// </summary>
    public async Task NotifyOrderStatusChange(string userId, string orderNumber, string newStatus)
    {
        await SendAsync(userId, "OrderUpdate",
            $"Order {orderNumber} - {newStatus}",
            $"Status order laundry Anda #{orderNumber} telah berubah menjadi: {newStatus}.",
            $"/orders/{orderNumber}");
    }

    /// <summary>
    /// Notifikasi untuk user: milik pribadi + broadcast (UserId null), terbaru dulu.
    /// </summary>
    public async Task<List<Notification>> GetForUserAsync(string userId, int take = 15)
    {
        return await _db.Notifications
            .Where(n => n.UserId == userId || n.UserId == null)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    /// <summary>
    /// Get unread notifications for user
    /// </summary>
    public async Task<List<Notification>> GetUnreadAsync(string userId)
    {
        return await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(20)
            .ToListAsync();
    }

    /// <summary>
    /// Mark notification as read
    /// </summary>
    public async Task MarkAsReadAsync(int notificationId)
    {
        var notification = await _db.Notifications.FindAsync(notificationId);
        if (notification != null)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Semua notifikasi user (pribadi + broadcast) dengan paging.
    /// </summary>
    public async Task<(List<Notification> Items, int Total)> GetPagedForUserAsync(string userId, int page = 1, int pageSize = 20, bool unreadOnly = false)
    {
        var query = _db.Notifications
            .Where(n => n.UserId == userId || n.UserId == null);
        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (items, total);
    }

    /// <summary>
    /// Tandai semua notifikasi user (termasuk broadcast yang tampil untuknya) sebagai dibaca.
    /// </summary>
    public async Task<int> MarkAllAsReadAsync(string userId)
    {
        return await _db.Notifications
            .Where(n => (n.UserId == userId || n.UserId == null) && !n.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }
}

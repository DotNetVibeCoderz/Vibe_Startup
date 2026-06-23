using Microsoft.EntityFrameworkCore;
using EventSphere.Data.Context;
using EventSphere.Data.Models;

namespace EventSphere.Services;

/// <summary>
/// Sistem notifikasi & pengingat
/// </summary>
public class NotificationService
{
    private readonly AppDbContext _db;
    public NotificationService(AppDbContext db) => _db = db;

    public async Task<List<Notification>> GetNotificationsForUserAsync(string userId, bool unreadOnly = false)
    {
        var query = _db.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        return await query.OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId) =>
        await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task<Notification> SendAsync(Notification notification)
    {
        notification.Id = Guid.NewGuid();
        notification.CreatedAt = DateTime.UtcNow;
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
        return notification;
    }

    public async Task SendToUserAsync(string userId, string message, string? type = "Info", string? actionUrl = null, Guid? eventId = null)
    {
        await SendAsync(new Notification
        {
            UserId = userId, Message = message, Type = type, ActionUrl = actionUrl, EventId = eventId
        });
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var n = await _db.Notifications.FindAsync(id);
        if (n != null) { n.IsRead = true; n.ReadAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, DateTime.UtcNow));
    }

    /// <summary>
    /// Cek task mendekati deadline dan kirim notifikasi
    /// </summary>
    public async Task CheckTaskDeadlinesAsync()
    {
        var soon = DateTime.UtcNow.AddDays(2);
        var tasks = await _db.TaskItems
            .Where(t => t.Status != TaskItemStatus.Done && t.DueDate <= soon && t.DueDate >= DateTime.UtcNow)
            .Include(t => t.Event)
            .ToListAsync();

        foreach (var t in tasks)
        {
            if (t.AssignedToId != null)
                await SendToUserAsync(t.AssignedToId, $"⏰ Deadline: '{t.Title}' untuk event '{t.Event?.Name}' jatuh tempo {t.DueDate:dd MMM yyyy}!", "Reminder", $"/events/{t.EventId}/tasks", t.EventId);
        }
    }
}

using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

public class NotificationService
{
    private readonly AppDbContext _db;
    public NotificationService(AppDbContext db) => _db = db;

    public async Task<List<Notification>> GetUserNotificationsAsync(string userId, bool unreadOnly = false)
    {
        var query = _db.Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        return await query.OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync();
    }

    public async Task SendAsync(string? userId, string title, string? message, NotificationType type, string? actionUrl = null)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId, Title = title, Message = message, Type = type, ActionUrl = actionUrl
        });
        await _db.SaveChangesAsync();
    }

    public async Task BroadcastAsync(string title, string? message, string? actionUrl = null)
    {
        var users = await _db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync();
        foreach (var uid in users)
            _db.Notifications.Add(new Notification { UserId = uid, Title = title, Message = message, Type = NotificationType.System, ActionUrl = actionUrl });
        await _db.SaveChangesAsync();
    }

    public async Task MarkAsReadAsync(int id) { var n = await _db.Notifications.FindAsync(id); if (n != null) { n.IsRead = true; await _db.SaveChangesAsync(); } }
    public async Task MarkAllReadAsync(string userId)
    {
        var unread = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        unread.ForEach(n => n.IsRead = true);
        await _db.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId) =>
        await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
}

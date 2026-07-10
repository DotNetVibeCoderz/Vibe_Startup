using Microsoft.EntityFrameworkCore;
using EstateHub.Data;
using EstateHub.Models;

namespace EstateHub.Services;

/// <summary>
/// Service for real-time chat between users
/// </summary>
public class ChatService
{
    private readonly AppDbContext _db;

    public ChatService(AppDbContext db) => _db = db;

    public async Task<ChatMessage> SendMessageAsync(ChatMessage message)
    {
        message.SentAt = DateTime.UtcNow;
        message.IsRead = false;
        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync();
        return message;
    }

    public async Task<List<ChatMessage>> GetConversationAsync(string userId1, string userId2, int take = 50)
    {
        return await _db.ChatMessages
            .Where(c => (c.SenderId == userId1 && c.ReceiverId == userId2) ||
                        (c.SenderId == userId2 && c.ReceiverId == userId1))
            .OrderByDescending(c => c.SentAt)
            .Take(take)
            .OrderBy(c => c.SentAt)
            .ToListAsync();
    }

    public async Task<List<ChatMessage>> GetUserInboxAsync(string userId)
    {
        // Get latest message from each conversation
        var sent = await _db.ChatMessages
            .Where(c => c.SenderId == userId)
            .GroupBy(c => c.ReceiverId)
            .Select(g => g.OrderByDescending(c => c.SentAt).First())
            .ToListAsync();

        var received = await _db.ChatMessages
            .Where(c => c.ReceiverId == userId)
            .GroupBy(c => c.SenderId)
            .Select(g => g.OrderByDescending(c => c.SentAt).First())
            .ToListAsync();

        return sent.Concat(received)
            .GroupBy(c => c.SenderId == userId ? c.ReceiverId : c.SenderId)
            .Select(g => g.OrderByDescending(c => c.SentAt).First())
            .OrderByDescending(c => c.SentAt)
            .ToList();
    }

    public async Task MarkAsReadAsync(string senderId, string receiverId)
    {
        var messages = await _db.ChatMessages
            .Where(c => c.SenderId == senderId && c.ReceiverId == receiverId && !c.IsRead)
            .ToListAsync();

        foreach (var msg in messages)
        {
            msg.IsRead = true;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _db.ChatMessages
            .CountAsync(c => c.ReceiverId == userId && !c.IsRead);
    }
}

/// <summary>
/// Service for reviews and ratings
/// </summary>
public class ReviewService
{
    private readonly AppDbContext _db;

    public ReviewService(AppDbContext db) => _db = db;

    public async Task<Review> AddReviewAsync(Review review)
    {
        review.CreatedAt = DateTime.UtcNow;
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();
        return review;
    }

    public async Task<List<Review>> GetPropertyReviewsAsync(int propertyId)
    {
        return await _db.Reviews
            .Where(r => r.PropertyId == propertyId)
            .Include(r => r.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<double> GetAverageRatingAsync(int propertyId)
    {
        var ratings = await _db.Reviews
            .Where(r => r.PropertyId == propertyId)
            .Select(r => r.Rating)
            .ToListAsync();
        return ratings.Count > 0 ? ratings.Average() : 0;
    }
}

/// <summary>
/// Service for real-time notifications
/// </summary>
public class NotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db) => _db = db;

    public async Task<Notification> SendNotificationAsync(Notification notification)
    {
        notification.CreatedAt = DateTime.UtcNow;
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
        return notification;
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(string userId, int take = 20)
    {
        return await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> GetUnreadNotificationCountAsync(string userId)
    {
        return await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task MarkAsReadAsync(long id)
    {
        var notification = await _db.Notifications.FindAsync(id);
        if (notification != null)
        {
            notification.IsRead = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();
        foreach (var n in notifications) n.IsRead = true;
        await _db.SaveChangesAsync();
    }
}

/// <summary>
/// Service for CRM lead management
/// </summary>
public class LeadService
{
    private readonly AppDbContext _db;

    public LeadService(AppDbContext db) => _db = db;

    public async Task<Lead> CreateLeadAsync(Lead lead)
    {
        lead.CreatedAt = DateTime.UtcNow;
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync();
        return lead;
    }

    public async Task<List<Lead>> GetAgentLeadsAsync(string agentId, string? status = null)
    {
        var query = _db.Leads.Where(l => l.AgentId == agentId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(l => l.Status == status);
        return await query.OrderByDescending(l => l.CreatedAt).ToListAsync();
    }

    public async Task UpdateLeadStatusAsync(long id, string status, string? notes = null)
    {
        var lead = await _db.Leads.FindAsync(id);
        if (lead != null)
        {
            lead.Status = status;
            if (notes != null) lead.Notes = notes;
            await _db.SaveChangesAsync();
        }
    }
}

/// <summary>
/// Service for advertising management
/// </summary>
public class AdvertisingService
{
    private readonly AppDbContext _db;

    public AdvertisingService(AppDbContext db) => _db = db;

    public async Task<Advertisement> CreateAdvertisementAsync(Advertisement ad)
    {
        ad.CreatedAt = DateTime.UtcNow;
        _db.Advertisements.Add(ad);
        await _db.SaveChangesAsync();
        return ad;
    }

    public async Task<List<Advertisement>> GetPropertyAdsAsync(int propertyId)
    {
        return await _db.Advertisements
            .Where(a => a.PropertyId == propertyId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task IncrementImpressionAsync(long adId)
    {
        var ad = await _db.Advertisements.FindAsync(adId);
        if (ad != null) { ad.Impressions++; await _db.SaveChangesAsync(); }
    }

    public async Task IncrementClickAsync(long adId)
    {
        var ad = await _db.Advertisements.FindAsync(adId);
        if (ad != null) { ad.Clicks++; await _db.SaveChangesAsync(); }
    }
}

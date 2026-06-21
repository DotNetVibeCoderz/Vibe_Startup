using Microsoft.EntityFrameworkCore;
using RentalBoil.Data;
using RentalBoil.Models;

namespace RentalBoil.Services;

public class NotificationService
{
    private readonly AppDbContext _db;
    public NotificationService(AppDbContext db) { _db = db; }

    public async Task<Notification> CreateNotificationAsync(string userId, string title, string message, string type = "info", string? link = null)
    { var n = new Notification { UserId = userId, Title = title, Message = message, Type = type, Link = link, IsRead = false, CreatedAt = DateTime.UtcNow }; _db.Notifications.Add(n); await _db.SaveChangesAsync(); return n; }

    public async Task<List<Notification>> GetUserNotificationsAsync(string userId, bool unreadOnly = false)
    { var q = _db.Notifications.Where(n => n.UserId == userId); if (unreadOnly) q = q.Where(n => !n.IsRead); return await q.OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync(); }

    public async Task<int> GetUnreadCountAsync(string userId) => await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task MarkAsReadAsync(int id) { var n = await _db.Notifications.FindAsync(id); if (n != null) { n.IsRead = true; n.ReadAt = DateTime.UtcNow; await _db.SaveChangesAsync(); } }
}

public class ChatService
{
    private readonly AppDbContext _db;
    public ChatService(AppDbContext db) { _db = db; }

    public async Task<ChatMessage> SendMessageAsync(string senderId, string receiverId, string message, int? bookingId = null)
    { var c = new ChatMessage { SenderId = senderId, ReceiverId = receiverId, Message = message, BookingId = bookingId, IsRead = false, SentAt = DateTime.UtcNow }; _db.ChatMessages.Add(c); await _db.SaveChangesAsync(); return c; }

    public async Task<List<ChatMessage>> GetConversationAsync(string user1Id, string user2Id, int? bookingId = null)
    { var q = _db.ChatMessages.Include(c => c.Sender).Include(c => c.Receiver).Where(c => (c.SenderId == user1Id && c.ReceiverId == user2Id) || (c.SenderId == user2Id && c.ReceiverId == user1Id)); if (bookingId.HasValue) q = q.Where(c => c.BookingId == bookingId.Value); return await q.OrderBy(c => c.SentAt).ToListAsync(); }
}

public class ReviewService
{
    private readonly AppDbContext _db;
    public ReviewService(AppDbContext db) { _db = db; }

    public async Task<Review> CreateReviewAsync(Review review)
    { _db.Reviews.Add(review); await _db.SaveChangesAsync(); var v = await _db.Vehicles.FindAsync(review.VehicleId); if (v != null) { var reviews = await _db.Reviews.Where(r => r.VehicleId == v.Id).ToListAsync(); v.AverageRating = Math.Round(reviews.Average(r => r.Rating), 1); v.ReviewCount = reviews.Count; await _db.SaveChangesAsync(); } return review; }

    public async Task<List<Review>> GetVehicleReviewsAsync(int vehicleId) => await _db.Reviews.Include(r => r.User).Where(r => r.VehicleId == vehicleId).OrderByDescending(r => r.CreatedAt).ToListAsync();
}

public class PaymentService
{
    private readonly AppDbContext _db;
    public PaymentService(AppDbContext db) { _db = db; }

    public async Task<Payment> CreatePaymentAsync(int bookingId, PaymentMethod method, decimal amount)
    { var p = new Payment { BookingId = bookingId, Method = method, Amount = amount, Status = PaymentStatus.Unpaid, ExpiresAt = DateTime.UtcNow.AddHours(24), CreatedAt = DateTime.UtcNow }; _db.Payments.Add(p); await _db.SaveChangesAsync(); return p; }

    public async Task<bool> ConfirmPaymentAsync(int bookingId, string externalTransactionId)
    { var b = await _db.Bookings.Include(x => x.Payment).FirstOrDefaultAsync(x => x.Id == bookingId); if (b == null) return false; b.PaymentStatus = PaymentStatus.Paid; b.PaidAt = DateTime.UtcNow; b.Status = BookingStatus.Confirmed; if (b.Payment != null) { b.Payment.Status = PaymentStatus.Paid; b.Payment.PaidAt = DateTime.UtcNow; b.Payment.ExternalTransactionId = externalTransactionId; } await _db.SaveChangesAsync(); return true; }
}

public class PromotionService
{
    private readonly AppDbContext _db;
    public PromotionService(AppDbContext db) { _db = db; }

    public async Task<List<Promotion>> GetActivePromotionsAsync() => await _db.Promotions.Where(p => p.IsActive && p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow).ToListAsync();

    /// <summary>
    /// Validasi kupon — case-insensitive
    /// </summary>
    public async Task<Promotion?> ValidateCouponAsync(string code, string? userTier = null)
    {
        var codeLower = code.ToLowerInvariant();
        var coupon = await _db.Promotions.FirstOrDefaultAsync(p => p.Code.ToLower() == codeLower && p.IsActive && p.StartDate <= DateTime.UtcNow && p.EndDate >= DateTime.UtcNow);
        if (coupon == null) return null;
        if (coupon.UsageLimit.HasValue && coupon.UsageCount >= coupon.UsageLimit.Value) return null;
        if (!string.IsNullOrWhiteSpace(coupon.RequiredTier) && coupon.RequiredTier != userTier) return null;
        return coupon;
    }
}

public class LoyaltyService
{
    private readonly AppDbContext _db;
    public LoyaltyService(AppDbContext db) { _db = db; }

    public async Task AwardPointsAsync(string userId, int points, string description, int? bookingId = null)
    { var u = await _db.Users.FindAsync(userId); if (u == null) return; _db.LoyaltyTransactions.Add(new LoyaltyTransaction { UserId = userId, Points = points, Type = "earn", Description = description, BookingId = bookingId }); u.LoyaltyPoints += points; u.MembershipTier = u.LoyaltyPoints switch { >= 5000 => "Platinum", >= 2000 => "Gold", >= 500 => "Silver", _ => "Basic" }; await _db.SaveChangesAsync(); }
}

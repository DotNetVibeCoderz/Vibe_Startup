// Hotel reviews: submission by customers, moderation by admins.
//
// The rating shown on the hotel card is derived, never edited by hand: it is
// recomputed from Approved reviews after every moderation decision. That is why
// approving or rejecting has to go through here rather than flipping a column.
using Microsoft.EntityFrameworkCore;
using Joka.Data;
using Joka.Models.Hotels;

namespace Joka.Services;

public record ReviewResult(bool Success, string Message);

public class ReviewService
{
    private readonly AppDbContext _db;

    public ReviewService(AppDbContext db) => _db = db;

    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    /// <summary>
    /// Files a review. It lands as Pending - nothing a customer writes appears
    /// on the public page before an admin has seen it.
    /// </summary>
    public async Task<ReviewResult> SubmitAsync(
        Guid hotelId, Guid userId, string? authorName,
        int rating, string? title, string? comment, string? pros, string? cons, DateTime? stayDate)
    {
        if (rating is < 1 or > 5)
            return new(false, "Rating harus antara 1 sampai 5.");

        if (string.IsNullOrWhiteSpace(comment) || comment.Trim().Length < 10)
            return new(false, "Tulis ulasan minimal 10 karakter.");

        var hotel = await _db.Hotels.FirstOrDefaultAsync(h => h.Id == hotelId);
        if (hotel is null)
            return new(false, "Hotel tidak ditemukan.");

        // One review per user per hotel, otherwise the rating is trivial to skew.
        var existing = await _db.HotelReviews
            .AnyAsync(r => r.HotelId == hotelId && r.UserId == userId);

        if (existing)
            return new(false, "Kamu sudah pernah menulis ulasan untuk properti ini.");

        // "Terverifikasi" means the reviewer really stayed here - checked against
        // their own bookings, not self-declared.
        var hasStayed = await _db.HotelBookings
            .AnyAsync(b => b.UserId == userId && b.Room!.HotelId == hotelId);

        _db.HotelReviews.Add(new HotelReview
        {
            HotelId = hotelId,
            UserId = userId,
            AuthorName = string.IsNullOrWhiteSpace(authorName) ? "Tamu Joka" : authorName.Trim(),
            Rating = rating,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            Comment = comment.Trim(),
            Pros = string.IsNullOrWhiteSpace(pros) ? null : pros.Trim(),
            Cons = string.IsNullOrWhiteSpace(cons) ? null : cons.Trim(),
            StayDate = stayDate ?? DateTime.UtcNow.Date,
            IsVerified = hasStayed,
            Status = Pending
        });

        await _db.SaveChangesAsync();

        return new(true, "Terima kasih! Ulasan kamu sedang ditinjau moderator dan akan tayang setelah disetujui.");
    }

    /// <summary>Approves or rejects a review, then rebuilds the hotel's rating.</summary>
    public async Task<ReviewResult> ModerateAsync(Guid reviewId, string decision, string moderator, string? note)
    {
        if (decision != Approved && decision != Rejected)
            return new(false, "Keputusan tidak dikenal.");

        var review = await _db.HotelReviews.FirstOrDefaultAsync(r => r.Id == reviewId);
        if (review is null)
            return new(false, "Ulasan tidak ditemukan.");

        if (decision == Rejected && string.IsNullOrWhiteSpace(note))
            return new(false, "Sertakan alasan penolakan.");

        review.Status = decision;
        review.ModeratedBy = moderator;
        review.ModeratedAt = DateTime.UtcNow;
        review.ModerationNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        review.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await RecalculateRatingAsync(review.HotelId);

        return new(true, decision == Approved ? "Ulasan ditayangkan." : "Ulasan ditolak.");
    }

    /// <summary>
    /// Rebuilds AverageRating and ReviewCount from Approved reviews only.
    /// Rejecting a review therefore really does pull its stars back out.
    /// </summary>
    public async Task RecalculateRatingAsync(Guid hotelId)
    {
        var hotel = await _db.Hotels.FirstOrDefaultAsync(h => h.Id == hotelId);
        if (hotel is null) return;

        var approved = await _db.HotelReviews
            .Where(r => r.HotelId == hotelId && r.Status == Approved)
            .Select(r => r.Rating)
            .ToListAsync();

        hotel.ReviewCount = approved.Count;
        hotel.AverageRating = approved.Count == 0 ? 0 : Math.Round(approved.Average(), 2);
        hotel.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    /// <summary>Public reviews for one hotel, newest first.</summary>
    public Task<List<HotelReview>> GetApprovedAsync(Guid hotelId, int take = 20) =>
        _db.HotelReviews.AsNoTracking()
            .Where(r => r.HotelId == hotelId && r.Status == Approved)
            .OrderByDescending(r => r.ModeratedAt ?? r.CreatedAt)
            .Take(take)
            .ToListAsync();

    /// <summary>Moderation queue. Pending first, then the most recently decided.</summary>
    public Task<List<HotelReview>> GetForModerationAsync(string? status = null, int take = 100)
    {
        var query = _db.HotelReviews.Include(r => r.Hotel).AsQueryable();

        if (!string.IsNullOrEmpty(status))
            query = query.Where(r => r.Status == status);

        return query
            .OrderBy(r => r.Status == Pending ? 0 : 1)
            .ThenByDescending(r => r.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public Task<int> PendingCountAsync() =>
        _db.HotelReviews.CountAsync(r => r.Status == Pending);
}

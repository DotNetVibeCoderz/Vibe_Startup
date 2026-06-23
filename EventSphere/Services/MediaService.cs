using Microsoft.EntityFrameworkCore;
using EventSphere.Data.Context;
using EventSphere.Data.Models;

namespace EventSphere.Services;

public class MediaService
{
    private readonly AppDbContext _db;
    public MediaService(AppDbContext db) => _db = db;

    public async Task<List<MediaItem>> GetMediaForEventAsync(Guid eventId, string? category = null)
    {
        var query = _db.MediaItems.Where(m => m.EventId == eventId);
        if (!string.IsNullOrEmpty(category)) query = query.Where(m => m.Category == category);
        return await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
    }

    public async Task<MediaItem> AddAsync(MediaItem item) { item.Id = Guid.NewGuid(); _db.MediaItems.Add(item); await _db.SaveChangesAsync(); return item; }
    public async Task<bool> DeleteAsync(Guid id) { var m = await _db.MediaItems.FindAsync(id); if (m == null) return false; _db.MediaItems.Remove(m); await _db.SaveChangesAsync(); return true; }

    // Documents
    public async Task<List<Document>> GetDocumentsForEventAsync(Guid eventId) =>
        await _db.Documents.Where(d => d.EventId == eventId).Include(d => d.UploadedBy).OrderByDescending(d => d.CreatedAt).ToListAsync();

    public async Task<Document> UploadDocumentAsync(Document doc) { doc.Id = Guid.NewGuid(); _db.Documents.Add(doc); await _db.SaveChangesAsync(); return doc; }
    public async Task<bool> DeleteDocumentAsync(Guid id) { var d = await _db.Documents.FindAsync(id); if (d == null) return false; _db.Documents.Remove(d); await _db.SaveChangesAsync(); return true; }

    // Feedback
    public async Task<List<Feedback>> GetFeedbackForEventAsync(Guid eventId) =>
        await _db.Feedbacks.Where(f => f.EventId == eventId).Include(f => f.User).OrderByDescending(f => f.CreatedAt).ToListAsync();

    public async Task<Feedback> SubmitFeedbackAsync(Feedback fb) { fb.Id = Guid.NewGuid(); _db.Feedbacks.Add(fb); await _db.SaveChangesAsync(); return fb; }

    // Forum
    public async Task<List<ForumPost>> GetForumPostsAsync(string? category = null)
    {
        var query = _db.ForumPosts.Include(p => p.Author).AsQueryable();
        if (!string.IsNullOrEmpty(category)) query = query.Where(p => p.Category == category);
        return await query.OrderByDescending(p => p.IsPinned).ThenByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<ForumPost?> GetForumPostAsync(Guid id) =>
        await _db.ForumPosts.Include(p => p.Author).Include(p => p.Comments).ThenInclude(c => c.Author).FirstOrDefaultAsync(p => p.Id == id);

    public async Task<ForumPost> CreatePostAsync(ForumPost post) { post.Id = Guid.NewGuid(); _db.ForumPosts.Add(post); await _db.SaveChangesAsync(); return post; }
    public async Task<ForumComment> AddCommentAsync(ForumComment comment) { comment.Id = Guid.NewGuid(); _db.ForumComments.Add(comment); await _db.SaveChangesAsync(); return comment; }

    // Loyalty
    public async Task<List<LoyaltyPoint>> GetLoyaltyForUserAsync(string userId) =>
        await _db.LoyaltyPoints.Where(l => l.UserId == userId).OrderByDescending(l => l.EarnedAt).ToListAsync();

    public async Task<int> GetTotalPointsAsync(string userId) =>
        await _db.LoyaltyPoints.Where(l => l.UserId == userId).SumAsync(l => l.Points);

    public async Task AddPointsAsync(string userId, int points, string description, string action)
    {
        _db.LoyaltyPoints.Add(new LoyaltyPoint { UserId = userId, Points = points, Description = description, Action = action });
        await _db.SaveChangesAsync();
    }
}

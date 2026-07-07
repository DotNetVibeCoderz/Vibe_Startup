using Microsoft.EntityFrameworkCore;
using FitnessCenter.Data;
using FitnessCenter.Models;

namespace FitnessCenter.Services;

public class ForumService
{
    private readonly AppDbContext _db;
    public ForumService(AppDbContext db) => _db = db;

    public async Task<List<ForumPost>> GetAllPostsAsync() =>
        await _db.ForumPosts.Include(p => p.User).Include(p => p.Comments).ThenInclude(c => c.User)
            .OrderByDescending(p => p.IsPinned).ThenByDescending(p => p.CreatedAt).ToListAsync();

    public async Task<ForumPost?> GetPostByIdAsync(int id) =>
        await _db.ForumPosts.Include(p => p.User).Include(p => p.Comments).ThenInclude(c => c.User)
            .Include(p => p.Reactions).FirstOrDefaultAsync(p => p.Id == id);

    public async Task<ForumPost> CreatePostAsync(ForumPost post) { _db.ForumPosts.Add(post); await _db.SaveChangesAsync(); return post; }
    public async Task UpdatePostAsync(ForumPost post) { post.UpdatedAt = DateTime.UtcNow; _db.ForumPosts.Update(post); await _db.SaveChangesAsync(); }
    public async Task DeletePostAsync(int id) { var p = await _db.ForumPosts.FindAsync(id); if (p != null) { _db.ForumPosts.Remove(p); await _db.SaveChangesAsync(); } }

    public async Task<ForumComment> AddCommentAsync(ForumComment comment) { _db.ForumComments.Add(comment); await _db.SaveChangesAsync(); return comment; }
    public async Task DeleteCommentAsync(int id) { var c = await _db.ForumComments.FindAsync(id); if (c != null) { _db.ForumComments.Remove(c); await _db.SaveChangesAsync(); } }

    public async Task<bool> ToggleReactionAsync(int? postId, int? commentId, string userId, string reactionType)
    {
        var existing = await _db.ForumReactions.FirstOrDefaultAsync(r => r.UserId == userId && r.PostId == postId && r.CommentId == commentId);
        if (existing != null) { _db.ForumReactions.Remove(existing); await _db.SaveChangesAsync(); return false; }

        _db.ForumReactions.Add(new ForumReaction { UserId = userId, PostId = postId, CommentId = commentId, ReactionType = reactionType });
        if (postId.HasValue) { var post = await _db.ForumPosts.FindAsync(postId.Value); if (post != null) post.Likes++; }
        if (commentId.HasValue) { var comment = await _db.ForumComments.FindAsync(commentId.Value); if (comment != null) comment.Likes++; }
        await _db.SaveChangesAsync();
        return true;
    }
}

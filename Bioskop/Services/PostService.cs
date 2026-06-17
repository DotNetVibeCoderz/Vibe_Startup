using Microsoft.EntityFrameworkCore;
using Bioskop.Data;
using Bioskop.Models;

namespace Bioskop.Services;

/// <summary>
/// Service untuk Curhat Film - Timeline seperti Twitter
/// </summary>
public class PostService
{
    private readonly ApplicationDbContext _context;

    public PostService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Mendapatkan postingan timeline dengan pagination
    /// </summary>
    public async Task<(List<Post> posts, bool hasMore)> GetTimelineAsync(int page = 0, int pageSize = 100)
    {
        var query = _context.Posts
            .Include(p => p.User)
            .Include(p => p.Comments).ThenInclude(c => c.User)
            .Include(p => p.Likes)
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.CreatedAt);

        var total = await query.CountAsync();
        var posts = await query.Skip(page * pageSize).Take(pageSize + 1).ToListAsync();
        var hasMore = posts.Count > pageSize;

        if (hasMore)
            posts = posts.Take(pageSize).ToList();

        return (posts, hasMore);
    }

    public async Task<Post?> GetByIdAsync(int id)
    {
        return await _context.Posts
            .Include(p => p.User)
            .Include(p => p.Comments).ThenInclude(c => c.User)
            .Include(p => p.Likes)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);
    }

    public async Task<Post> CreatePostAsync(Post post)
    {
        _context.Posts.Add(post);
        await _context.SaveChangesAsync();
        return post;
    }

    public async Task<bool> DeletePostAsync(int postId, string userId)
    {
        var post = await _context.Posts.FindAsync(postId);
        if (post == null || post.UserId != userId) return false;

        post.IsDeleted = true;
        post.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Like / Unlike postingan
    /// </summary>
    public async Task<(bool success, int likeCount)> ToggleLikeAsync(int postId, string userId)
    {
        var existing = await _context.Likes
            .FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

        if (existing != null)
        {
            _context.Likes.Remove(existing);
            await _context.SaveChangesAsync();
            var count = await _context.Likes.CountAsync(l => l.PostId == postId);
            return (true, count);
        }
        else
        {
            _context.Likes.Add(new Like { PostId = postId, UserId = userId });
            await _context.SaveChangesAsync();
            var count = await _context.Likes.CountAsync(l => l.PostId == postId);
            return (true, count);
        }
    }

    public async Task<bool> HasUserLikedAsync(int postId, string userId)
    {
        return await _context.Likes.AnyAsync(l => l.PostId == postId && l.UserId == userId);
    }

    public async Task<int> GetLikeCountAsync(int postId)
    {
        return await _context.Likes.CountAsync(l => l.PostId == postId);
    }

    /// <summary>
    /// Tambah komentar ke postingan
    /// </summary>
    public async Task<Comment> AddCommentAsync(int postId, string userId, string content, string? attachedImages = null)
    {
        var comment = new Comment
        {
            PostId = postId,
            UserId = userId,
            Content = content,
            AttachedImages = attachedImages
        };
        _context.Comments.Add(comment);
        await _context.SaveChangesAsync();

        return await _context.Comments
            .Include(c => c.User)
            .FirstAsync(c => c.Id == comment.Id);
    }

    public async Task<bool> DeleteCommentAsync(int commentId, string userId)
    {
        var comment = await _context.Comments.FindAsync(commentId);
        if (comment == null || comment.UserId != userId) return false;

        comment.IsDeleted = true;
        await _context.SaveChangesAsync();
        return true;
    }
}

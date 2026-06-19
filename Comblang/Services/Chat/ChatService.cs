using Comblang.Data;
using Comblang.Models;
using Microsoft.EntityFrameworkCore;

namespace Comblang.Services.Chat;

public class ChatService
{
    private readonly AppDbContext _db;
    public ChatService(AppDbContext db) => _db = db;

    public async Task<Message> SendMessageAsync(Guid senderId, Guid receiverId, string content, string messageType = "Text", string? mediaUrl = null)
    {
        bool isMatched = await _db.Matches.AnyAsync(m => ((m.UserId1 == senderId && m.UserId2 == receiverId) || (m.UserId1 == receiverId && m.UserId2 == senderId)) && m.IsActive);
        if (!isMatched) throw new InvalidOperationException("Kalian belum match!");
        var msg = new Message { SenderId = senderId, ReceiverId = receiverId, Content = content, MessageType = messageType, MediaUrl = mediaUrl, SentAt = DateTime.UtcNow };
        _db.Messages.Add(msg); await _db.SaveChangesAsync(); return msg;
    }

    public async Task<List<Message>> GetConversationAsync(Guid u1, Guid u2, int page = 1, int pageSize = 50) =>
        await _db.Messages.Include(m => m.Sender).Where(m => (m.SenderId == u1 && m.ReceiverId == u2) || (m.SenderId == u2 && m.ReceiverId == u1)).OrderByDescending(m => m.SentAt).Skip((page - 1) * pageSize).Take(pageSize).OrderBy(m => m.SentAt).ToListAsync();

    public async Task<List<Message>> GetConversationWithUsersAsync(Guid u1, Guid u2, int skip = 0, int take = 50) =>
        await _db.Messages.Include(m => m.Sender).Include(m => m.Receiver).Where(m => (m.SenderId == u1 && m.ReceiverId == u2) || (m.SenderId == u2 && m.ReceiverId == u1)).OrderByDescending(m => m.SentAt).Skip(skip).Take(take).OrderBy(m => m.SentAt).ToListAsync();

    public async Task<List<Match>> GetUserMatchesAsync(Guid userId) =>
        await _db.Matches.Include(m => m.User1)!.ThenInclude(u => u!.Profile).Include(m => m.User2)!.ThenInclude(u => u!.Profile).Where(m => (m.UserId1 == userId || m.UserId2 == userId) && m.IsActive).OrderByDescending(m => m.MatchedAt).ToListAsync();

    /// <summary>
    /// Returns match list with last message + unread. 
    /// Avatar is: ProfilePictureUrl if exists, else gender emoji, else 👤.
    /// </summary>
    public async Task<List<ChatItem>> GetMatchListWithLastMessageAsync(Guid userId)
    {
        var matches = await _db.Matches.Include(m => m.User1)!.ThenInclude(u => u!.Profile).Include(m => m.User2)!.ThenInclude(u => u!.Profile).Where(m => (m.UserId1 == userId || m.UserId2 == userId) && m.IsActive).OrderByDescending(m => m.MatchedAt).ToListAsync();
        var result = new List<ChatItem>();
        foreach (var match in matches)
        {
            var matchedUser = match.UserId1 == userId ? match.User2! : match.User1!;
            var lastMsg = await _db.Messages.Where(m => (m.SenderId == userId && m.ReceiverId == matchedUser.Id) || (m.SenderId == matchedUser.Id && m.ReceiverId == userId)).OrderByDescending(m => m.SentAt).FirstOrDefaultAsync();
            int unread = await _db.Messages.CountAsync(m => m.SenderId == matchedUser.Id && m.ReceiverId == userId && !m.IsRead);
            var content = lastMsg?.Content ?? "";
            if (content.Length > 60) content = string.Concat(content.AsSpan(0, 60), "...");

            // 🔑 Avatar: URL gambar kalau ada, else emoji gender, else 👤
            string avatar;
            var pic = matchedUser.Profile?.ProfilePictureUrl;
            if (!string.IsNullOrWhiteSpace(pic)) avatar = pic;
            else if (matchedUser.Profile?.Gender == "Female") avatar = "👩";
            else if (matchedUser.Profile?.Gender == "Male") avatar = "👨";
            else avatar = "👤";

            result.Add(new ChatItem { Id = matchedUser.Id, Name = matchedUser.Username, Avatar = avatar, LastMessage = content, Unread = unread, LastMessageAt = lastMsg?.SentAt ?? match.MatchedAt, IsOnline = false, IsGroup = false });
        }
        return result;
    }

    public async Task MarkMessagesAsReadAsync(Guid readerId, Guid senderId)
    {
        var unread = await _db.Messages.Where(m => m.SenderId == senderId && m.ReceiverId == readerId && !m.IsRead).ToListAsync();
        foreach (var m in unread) m.IsRead = true;
        if (unread.Count > 0) await _db.SaveChangesAsync();
    }
}

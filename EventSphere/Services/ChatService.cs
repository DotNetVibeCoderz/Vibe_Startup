using Microsoft.EntityFrameworkCore;
using EventSphere.Data.Context;
using EventSphere.Data.Models;

namespace EventSphere.Services;

public class ChatService
{
    private readonly AppDbContext _db;
    public ChatService(AppDbContext db) => _db = db;

    // Sessions
    public async Task<List<ChatSession>> GetSessionsForUserAsync(string userId) =>
        await _db.ChatSessions.Where(cs => cs.Members.Any(m => m.UserId == userId) && cs.IsActive)
            .Include(cs => cs.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .OrderByDescending(cs => cs.Messages.Max(m => (DateTime?)m.SentAt))
            .ToListAsync();

    public async Task<ChatSession?> GetSessionAsync(Guid id) =>
        await _db.ChatSessions.Include(cs => cs.Members).ThenInclude(m => m.User)
            .Include(cs => cs.Messages.OrderBy(m => m.SentAt)).ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(cs => cs.Id == id);

    public async Task<ChatSession> CreateSessionAsync(ChatSession session)
    {
        session.Id = Guid.NewGuid();
        session.CreatedAt = DateTime.UtcNow;
        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    // Messages
    public async Task<ChatMessage> SendMessageAsync(ChatMessage msg)
    {
        msg.Id = Guid.NewGuid();
        msg.SentAt = DateTime.UtcNow;
        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync();
        return msg;
    }

    public async Task<List<ChatMessage>> GetMessagesForSessionAsync(Guid sessionId) =>
        await _db.ChatMessages.Where(m => m.ChatSessionId == sessionId)
            .Include(m => m.Sender).OrderBy(m => m.SentAt).ToListAsync();

    // Members
    public async Task AddMemberAsync(Guid sessionId, string userId)
    {
        if (!await _db.ChatSessionMembers.AnyAsync(m => m.SessionId == sessionId && m.UserId == userId))
        {
            _db.ChatSessionMembers.Add(new ChatSessionMember { SessionId = sessionId, UserId = userId });
            await _db.SaveChangesAsync();
        }
    }
}

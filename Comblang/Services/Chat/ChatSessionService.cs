using Comblang.Data;
using Comblang.Models;
using Microsoft.EntityFrameworkCore;

namespace Comblang.Services.Chat;

/// <summary>
/// Manages Si Mak Comblang AI chat sessions and messages.
/// Sessions can be tied to a registered user or be anonymous (UserId = null).
/// </summary>
public class ChatSessionService
{
    private readonly AppDbContext _db;

    public ChatSessionService(AppDbContext db)
    {
        _db = db;
    }

    // ──────────────────────────────────────────────
    //  Session CRUD
    // ──────────────────────────────────────────────

    /// <summary>
    /// Creates a new chat session and returns it.
    /// If userId is null, the session is anonymous.
    /// </summary>
    public async Task<ChatSession> CreateSessionAsync(Guid? userId, string title)
    {
        var session = new ChatSession
        {
            UserId = userId,
            SessionTitle = string.IsNullOrWhiteSpace(title) ? "Percakapan Baru" : title,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    /// <summary>
    /// Returns all active sessions for a given user (or anonymous sessions if userId is null).
    /// Ordered by most recent first.
    /// </summary>
    public async Task<List<ChatSession>> GetUserSessionsAsync(Guid? userId)
    {
        return await _db.ChatSessions
            .Where(s => s.UserId == userId && s.IsActive)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Returns all messages for a given session, ordered chronologically.
    /// </summary>
    public async Task<List<ChatMessage>> GetSessionMessagesAsync(Guid sessionId)
    {
        return await _db.ChatMessages
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Adds a message to a session and returns the created message.
    /// Also updates the session title based on the first user message.
    /// </summary>
    public async Task<ChatMessage> AddMessageAsync(
        Guid sessionId,
        string role,
        string content,
        string? imageUrl = null)
    {
        var safeContent = content ?? string.Empty;

        var message = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = role,
            Content = safeContent,
            ImageUrl = imageUrl,
            CreatedAt = DateTime.UtcNow
        };

        _db.ChatMessages.Add(message);

        // Auto-title: use first user message (truncated) as session title
        if (role == "user")
        {
            var session = await _db.ChatSessions.FindAsync(sessionId);
            if (session != null)
            {
                var messageCount = await _db.ChatMessages
                    .CountAsync(m => m.ChatSessionId == sessionId && m.Role == "user");

                if (messageCount == 0) // This is the first user message (the one we just added is not yet saved)
                {
                    var title = safeContent.Length > 60
                        ? string.Concat(safeContent.AsSpan(0, 60), "...")
                        : safeContent;
                    session.SessionTitle = title;
                }
            }
        }

        await _db.SaveChangesAsync();
        return message;
    }

    /// <summary>
    /// Soft-deletes a session by marking it inactive.
    /// </summary>
    public async Task DeleteSessionAsync(Guid sessionId)
    {
        var session = await _db.ChatSessions.FindAsync(sessionId);
        if (session != null)
        {
            session.IsActive = false;
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Clears all messages from a session but keeps the session itself.
    /// </summary>
    public async Task ResetSessionAsync(Guid sessionId)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.ChatSessionId == sessionId)
            .ToListAsync();

        _db.ChatMessages.RemoveRange(messages);
        await _db.SaveChangesAsync();
    }
}

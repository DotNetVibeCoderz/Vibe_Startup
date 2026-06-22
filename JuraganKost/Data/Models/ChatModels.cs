using System.ComponentModel.DataAnnotations;

namespace JuraganKost.Data.Models;

/// <summary>
/// Persisted chat thread (session) in database.
/// UserId is a plain string (no FK constraint) to avoid issues with unauthenticated sessions.
/// </summary>
public class ChatThread
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    [MaxLength(450)]
    public string? UserId { get; set; }
    [MaxLength(20)]
    public string Provider { get; set; } = "OpenAI";
    [MaxLength(200)]
    public string Title { get; set; } = "Sesi baru";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    public ICollection<ChatMessageDb> Messages { get; set; } = new List<ChatMessageDb>();
}

/// <summary>
/// Persisted chat message in database
/// </summary>
public class ChatMessageDb
{
    public int Id { get; set; }
    public int ChatThreadId { get; set; }
    public ChatThread? Thread { get; set; }
    [MaxLength(20)]
    public string Role { get; set; } = "user";
    [MaxLength(8000)]
    public string Content { get; set; } = string.Empty;
    [MaxLength(2000)]
    public string? ImageUrl { get; set; }
    [MaxLength(2000)]
    public string? DocumentUrl { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

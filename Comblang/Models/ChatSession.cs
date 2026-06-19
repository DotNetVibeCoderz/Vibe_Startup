using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Comblang.Models;

/// <summary>
/// Represents a chat session with Si Mak Comblang AI assistant.
/// Each session belongs to an optional user (nullable for anonymous users)
/// and contains a list of chat messages.
/// </summary>
public class ChatSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The user who owns this session. Null for anonymous users.
    /// </summary>
    public Guid? UserId { get; set; }

    [Required, MaxLength(200)]
    public string SessionTitle { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // ── Navigation ──

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

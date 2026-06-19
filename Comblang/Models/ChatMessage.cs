using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Comblang.Models;

/// <summary>
/// Represents a single message within a Si Mak Comblang chat session.
/// The role indicates whether the message came from the user or the AI assistant.
/// </summary>
public class ChatMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key to the parent chat session.
    /// </summary>
    [Required]
    public Guid ChatSessionId { get; set; }

    /// <summary>
    /// Role of the message sender: "user" or "assistant".
    /// </summary>
    [Required, MaxLength(20)]
    public string Role { get; set; } = "user";

    /// <summary>
    /// The text content of the message. Maximum 5000 characters.
    /// </summary>
    [MaxLength(5000)]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional URL to an image attached to this message.
    /// </summary>
    [MaxLength(2000)]
    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ──

    [ForeignKey(nameof(ChatSessionId))]
    public ChatSession? Session { get; set; }
}

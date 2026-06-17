using System.ComponentModel.DataAnnotations;

namespace Bioskop.Models;

/// <summary>
/// Session chat dengan Si Bobby Movie Maniac
/// </summary>
public class ChatSession
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; } // Judul session otomatis dari pesan pertama

    public string UserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastActivityAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ApplicationUser? User { get; set; }
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

/// <summary>
/// Pesan dalam chat session
/// </summary>
public class ChatMessage
{
    public long Id { get; set; }
    public int ChatSessionId { get; set; }

    [MaxLength(20)]
    public string Role { get; set; } = "user"; // user, assistant, system

    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// JSON: metadata tambahan (attached images, documents, function calls, dll)
    /// </summary>
    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ChatSession? ChatSession { get; set; }
}

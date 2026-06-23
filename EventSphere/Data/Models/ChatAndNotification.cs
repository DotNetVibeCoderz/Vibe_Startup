using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventSphere.Data.Models;

/// <summary>
/// Chat & messaging real-time
/// </summary>
public class ChatMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid? EventId { get; set; }
    
    [Required]
    public string SenderId { get; set; } = string.Empty;
    
    [MaxLength(5000)]
    public string? Content { get; set; }
    
    [MaxLength(50)]
    public string MessageType { get; set; } = "Text"; // Text, Image, File, System
    
    [MaxLength(500)]
    public string? AttachmentUrl { get; set; }
    
    [MaxLength(200)]
    public string? AttachmentName { get; set; }
    
    public Guid? ChatSessionId { get; set; }
    
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    
    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }
    
    [ForeignKey(nameof(SenderId))]
    public ApplicationUser? Sender { get; set; }
    
    [ForeignKey(nameof(ChatSessionId))]
    public ChatSession? Session { get; set; }
}

/// <summary>
/// Session chat per event atau per group
/// </summary>
public class ChatSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [MaxLength(200)]
    public string? Name { get; set; }
    
    public Guid? EventId { get; set; }
    
    [MaxLength(50)]
    public string SessionType { get; set; } = "Event"; // Event, Team, Vendor, Client, Bot
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    
    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }
    
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    public ICollection<ChatSessionMember> Members { get; set; } = new List<ChatSessionMember>();
}

/// <summary>
/// Anggota chat session
/// </summary>
public class ChatSessionMember
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid SessionId { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey(nameof(SessionId))]
    public ChatSession? Session { get; set; }
    
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}

/// <summary>
/// Notifikasi sistem
/// </summary>
public class Notification
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required, MaxLength(500)]
    public string Message { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? Type { get; set; } // Info, Warning, Success, Error, Reminder
    
    [MaxLength(500)]
    public string? ActionUrl { get; set; }
    
    public Guid? EventId { get; set; }
    
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
    
    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }
}

/// <summary>
/// Program loyalitas
/// </summary>
public class LoyaltyPoint
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public int Points { get; set; }
    
    [MaxLength(200)]
    public string? Description { get; set; }
    
    [MaxLength(50)]
    public string? Action { get; set; } // EventBooked, ReviewSubmitted, Referral, etc.
    
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}

/// <summary>
/// Forum komunitas
/// </summary>
public class ForumPost
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(5000)]
    public string? Content { get; set; }
    
    [Required]
    public string AuthorId { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? Category { get; set; }
    
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey(nameof(AuthorId))]
    public ApplicationUser? Author { get; set; }
    
    public ICollection<ForumComment> Comments { get; set; } = new List<ForumComment>();
}

/// <summary>
/// Komentar forum
/// </summary>
public class ForumComment
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid PostId { get; set; }
    
    [Required]
    public string AuthorId { get; set; } = string.Empty;
    
    [MaxLength(3000)]
    public string? Content { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey(nameof(PostId))]
    public ForumPost? Post { get; set; }
    
    [ForeignKey(nameof(AuthorId))]
    public ApplicationUser? Author { get; set; }
}

/// <summary>
/// AI Chat bot session untuk "Tante Sherly"
/// </summary>
public class ChatBotSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [MaxLength(200)]
    public string? Title { get; set; } = "New Chat";
    
    [MaxLength(100)]
    public string? ModelProvider { get; set; } // OpenAI, Anthropic, Gemini, Ollama
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastActivity { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
    
    public ICollection<ChatBotMessage> Messages { get; set; } = new List<ChatBotMessage>();
}

/// <summary>
/// Pesan dalam chat bot session
/// </summary>
public class ChatBotMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid SessionId { get; set; }
    
    [MaxLength(100)]
    public string Role { get; set; } = "User"; // User, Assistant, System
    
    [MaxLength(10000)]
    public string? Content { get; set; }
    
    [MaxLength(2000)]
    public string? ImageUrl { get; set; }
    
    [MaxLength(500)]
    public string? AttachmentUrl { get; set; }
    
    [MaxLength(200)]
    public string? AttachmentName { get; set; }
    
    public int? TokenCount { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey(nameof(SessionId))]
    public ChatBotSession? Session { get; set; }
}

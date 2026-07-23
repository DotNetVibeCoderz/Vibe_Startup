using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeWallet.Models;

/// <summary>
/// Chat session (conversation thread)
/// </summary>
public class ChatSession : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = "New Chat";

    public ChatProvider Provider { get; set; } = ChatProvider.OpenAI;

    [MaxLength(100)]
    public string ModelId { get; set; } = "gpt-4o";

    [Column(TypeName = "decimal(3,2)")]
    public decimal Temperature { get; set; } = 0.7m;

    [MaxLength(4000)]
    public string? SystemPrompt { get; set; }

    public bool IsActive { get; set; } = true;

    public int MessageCount { get; set; } = 0;

    public DateTime? LastMessageAt { get; set; }

    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

/// <summary>
/// Chat message
/// </summary>
public class ChatMessage : BaseEntity
{
    [Required]
    public Guid ChatSessionId { get; set; }

    [ForeignKey(nameof(ChatSessionId))]
    public virtual ChatSession? ChatSession { get; set; }

    [MaxLength(20)]
    public string Role { get; set; } = "user"; // "user", "assistant", "system", "tool"

    [MaxLength(8000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? RenderedContent { get; set; } // Markdown rendered to HTML

    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }

    [Column(TypeName = "decimal(10,6)")]
    public decimal? Cost { get; set; }

    [MaxLength(500)]
    public string? FunctionName { get; set; }

    public string? FunctionArguments { get; set; }

    public string? FunctionResult { get; set; }

    public virtual ICollection<ChatAttachment> Attachments { get; set; } = new List<ChatAttachment>();
}

/// <summary>
/// Chat attachment (image, document, etc.)
/// </summary>
public class ChatAttachment : BaseEntity
{
    [Required]
    public Guid ChatMessageId { get; set; }

    [ForeignKey(nameof(ChatMessageId))]
    public virtual ChatMessage? ChatMessage { get; set; }

    public AttachmentType Type { get; set; }

    [MaxLength(300)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FileUrl { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [MaxLength(1000)]
    public string? Description { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WashUp.Models;

/// <summary>
/// Chat session for Mbok Inem AI chatbot
/// </summary>
public class ChatSession
{
    public int Id { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    
    [Required, MaxLength(200)]
    public string Title { get; set; } = "New Chat";
    
    [MaxLength(50)]
    public string ModelProvider { get; set; } = "OpenAI"; // OpenAI, Anthropic, Gemini, Ollama
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastMessageAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

/// <summary>
/// Individual chat message
/// </summary>
public class ChatMessage
{
    public int Id { get; set; }
    public int ChatSessionId { get; set; }
    public ChatSession? ChatSession { get; set; }
    
    [MaxLength(20)]
    public string Role { get; set; } = "user"; // user, assistant, system
    
    [Column(TypeName = "text")]
    public string Content { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? ModelUsed { get; set; }
    
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    
    // Attachment info
    [MaxLength(1000)]
    public string? ImageUrl { get; set; }
    
    [MaxLength(1000)]
    public string? DocumentUrl { get; set; }
    
    [MaxLength(200)]
    public string? DocumentName { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

using System.ComponentModel.DataAnnotations;

namespace PDA.Models;

/// <summary>
/// Chat session for multi-session support
/// </summary>
public class ChatSession
{
    public int Id { get; set; }

    [Required, MaxLength(300)]
    public string Title { get; set; } = "New Chat";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // LLM Configuration for this session
    [MaxLength(50)]
    public string ModelProvider { get; set; } = "OpenAI"; // OpenAI, Anthropic, Gemini, Ollama, Custom

    [MaxLength(100)]
    public string ModelName { get; set; } = "gpt-4o";

    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 4096;

    [MaxLength(4000)]
    public string? SystemPrompt { get; set; }

    // Foreign keys
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public int? DatabaseConnectionId { get; set; }

    // Navigation
    public ApplicationUser? User { get; set; }
    public DatabaseConnection? DatabaseConnection { get; set; }
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

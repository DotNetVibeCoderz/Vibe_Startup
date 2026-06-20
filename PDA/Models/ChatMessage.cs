using System.ComponentModel.DataAnnotations;

namespace PDA.Models;

/// <summary>
/// Individual chat message within a session
/// </summary>
public class ChatMessage
{
    public int Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [Required, MaxLength(20)]
    public string Role { get; set; } = "user"; // user, assistant, system, tool

    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Optional HTML/rendered dashboard content
    /// </summary>
    public string? DashboardHtml { get; set; }

    /// <summary>
    /// Token usage for this message
    /// </summary>
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }

    /// <summary>
    /// Time taken to generate response
    /// </summary>
    public double? ResponseTimeMs { get; set; }

    /// <summary>
    /// File attachments (JSON array of URLs)
    /// </summary>
    public string? Attachments { get; set; }

    // Foreign key
    public int ChatSessionId { get; set; }

    // Navigation
    public ChatSession? ChatSession { get; set; }
}

/// <summary>
/// Message attachment reference
/// </summary>
public class MessageAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public bool IsImage { get; set; }
}

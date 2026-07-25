using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ngibrid.Models;

/// <summary>
/// Chat session for AI chatbot "Mas Supri"
/// </summary>
public class ChatSession : BaseEntity
{
    public long UserId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "New Chat";

    [MaxLength(50)]
    public string Model { get; set; } = "OpenAI";

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? SystemPrompt { get; set; }

    public double Temperature { get; set; } = 0.7;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    public ICollection<ChatMessage>? Messages { get; set; }
}

/// <summary>
/// Chat message within a session
/// </summary>
public class ChatMessage : BaseEntity
{
    public long ChatSessionId { get; set; }

    [Required]
    public string Role { get; set; } = "user"; // user, assistant, system, tool

    [Required]
    public string Content { get; set; } = string.Empty;

    public int TokenCount { get; set; }

    /// <summary>
    /// JSON list of attached file URLs
    /// </summary>
    public string? AttachmentsJson { get; set; }

    [MaxLength(200)]
    public string? ModelUsed { get; set; }

    public double? PromptTokens { get; set; }
    public double? CompletionTokens { get; set; }

    [ForeignKey(nameof(ChatSessionId))]
    public ChatSession? Session { get; set; }
}

/// <summary>
/// Audit log for all system activities
/// </summary>
public class AuditLog : BaseEntity
{
    [Required, MaxLength(256)]
    public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string EntityType { get; set; } = string.Empty;

    public long? EntityId { get; set; }

    [MaxLength(100)]
    public string? PropertyName { get; set; }

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }
}

/// <summary>
/// Analytics data point
/// </summary>
public class AnalyticsData : BaseEntity
{
    public DateTime Period { get; set; }

    [MaxLength(50)]
    public string MetricName { get; set; } = string.Empty;

    public double MetricValue { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string PeriodType { get; set; } = "DAILY"; // HOURLY, DAILY, WEEKLY, MONTHLY, YEARLY
}

/// <summary>
/// System configuration (editable from UI)
/// </summary>
public class SystemConfiguration : BaseEntity
{
    [Required, MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; } // General, Payment, Shipping, AI, etc.

    public bool IsEditable { get; set; } = true;
}

/// <summary>
/// Notification entity
/// </summary>
public class Notification : BaseEntity
{
    public long UserId { get; set; }

    [Required, MaxLength(50)]
    public string Type { get; set; } = "INFO"; // INFO, WARNING, SUCCESS, ERROR

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;

    [MaxLength(500)]
    public string? ActionUrl { get; set; }

    [MaxLength(50)]
    public string? Channel { get; set; } // WEB, EMAIL, SMS, PUSH

    public DateTime? ReadAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}

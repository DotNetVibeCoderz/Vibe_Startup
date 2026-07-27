// Chat-related models for Mas Bolang chatbot
using Joka.Models.Common;

namespace Joka.Models.Chat;

public class ChatSession : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = "New Chat";
    public string ModelProvider { get; set; } = "OpenAI";
    public string ModelName { get; set; } = "gpt-4o-mini";
    public double Temperature { get; set; } = 0.7;
    public bool IsActive { get; set; } = true;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ChatMessage : BaseEntity
{
    public Guid ChatSessionId { get; set; }
    public ChatSession? Session { get; set; }
    public string Role { get; set; } = "user"; // user, assistant, system, tool
    public string Content { get; set; } = string.Empty;
    public string? ToolCallName { get; set; }
    public string? ToolCallResult { get; set; }
    public int TokenCount { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class ChatAttachment : BaseEntity
{
    public Guid ChatMessageId { get; set; }
    public ChatMessage? Message { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = "image"; // image, document, audio, video
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
}

public class ChatBotConfig
{
    public string Name { get; set; } = "Mas Bolang";
    public string Description { get; set; } = string.Empty;
    public string Provider { get; set; } = "OpenAI";
    public string DefaultModel { get; set; } = "gpt-4o-mini";
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 4096;
    public string SystemPrompt { get; set; } = string.Empty;
    public Dictionary<string, ProviderConfig> Providers { get; set; } = new();
}

public class ProviderConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
}

public class ChatRequest
{
    public Guid? SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string>? Attachments { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
}

public class ChatResponse
{
    public Guid SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ToolCalls { get; set; }
    public int TokenCount { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

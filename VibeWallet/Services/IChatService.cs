using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Service for AI Chat Bot "Mbak Selvi" using Semantic Kernel
/// Supports: OpenAI, Anthropic, Gemini, Ollama
/// </summary>
public interface IChatService
{
    // Session Management
    Task<ChatSession> CreateSessionAsync(Guid userId, string? title = null, ChatProvider provider = ChatProvider.OpenAI);
    Task<ChatSession?> GetSessionAsync(Guid sessionId);
    Task<List<ChatSession>> GetUserSessionsAsync(Guid userId);
    Task<bool> DeleteSessionAsync(Guid sessionId);
    Task<bool> ResetSessionAsync(Guid sessionId);

    // Messaging
    Task<ChatMessage> SendMessageAsync(Guid sessionId, string message, List<ChatAttachment>? attachments = null);
    Task<List<ChatMessage>> GetSessionMessagesAsync(Guid sessionId);

    // Attachment
    Task<ChatAttachment> AddAttachmentAsync(Guid messageId, AttachmentType type, string fileName, string fileUrl, string contentType, long fileSize);

    // Model & Configuration
    Task<List<string>> GetAvailableModelsAsync();
    Task UpdateSessionConfigAsync(Guid sessionId, ChatProvider provider, string modelId, decimal temperature);

    // Kernel Functions
    Task<string> SearchInternetAsync(string query);
    Task<string> ScrapWebPageAsync(string url);
    Task<string> ReadFileFromUrlAsync(string url);
    Task<string> GetCurrentDateTimeAsync(string timezone = "Asia/Jakarta");
    Task<string> CalculateMathAsync(string expression);
    Task<string> QueryDatabaseAsync(string queryContext);

    // Markdown Rendering
    Task<string> RenderMarkdownAsync(string markdownText);
}

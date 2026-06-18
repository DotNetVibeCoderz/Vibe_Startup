using LandLord.Models;

namespace LandLord.Services;

/// <summary>
/// Interface untuk chat bot service dengan Kernel Functions & Attachment support
/// </summary>
public interface IChatService
{
    // Session management
    Task<List<ChatSession>> GetSessionsAsync(string? userId);
    Task<ChatSession> CreateSessionAsync(string? userId, string title = "Chat Baru");
    Task<ChatSession?> GetSessionAsync(int sessionId);
    Task<bool> DeleteSessionAsync(int sessionId);
    Task<bool> ResetSessionAsync(int sessionId);
    Task<List<ChatMessage>> GetMessagesAsync(int sessionId);

    // Messaging dengan attachment
    Task<ChatMessage> SendMessageAsync(int sessionId, string content,
        string? imageUrl = null, string? documentUrl = null, string? documentName = null);

    // AI Response — sekarang support imageUrl & documentUrl untuk ImageContent
    Task<ChatResponse> GetAIResponseAsync(int sessionId, string userMessage,
        string? imageUrl = null, string? documentUrl = null, string? documentName = null);

    // Upload file attachment dan return URL publik
    Task<string?> UploadAttachmentAsync(Stream fileStream, string fileName, string contentType);

    // Kernel Functions
    List<KernelFunctionDefinition> GetAvailableFunctions();
    Task<FunctionResult> ExecuteFunctionAsync(int sessionId, string functionName, Dictionary<string, object?> parameters);
    bool ShouldSearchInternet(string userMessage);
}

/// <summary>
/// Response dari AI chat
/// </summary>
public class ChatResponse
{
    public string Content { get; set; } = string.Empty;
    public bool UsedInternetSearch { get; set; }
    public bool UsedDatabaseQuery { get; set; }
    public string? FunctionCalled { get; set; }
    public List<string> Sources { get; set; } = new();
}

using BlazePoint.Data;
using BlazePoint.Services.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text.Json;

namespace BlazePoint.Services.Clippy;

public class ClippyService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IConfiguration config,
    IHttpClientFactory httpFactory,
    IFileStorage storage,
    IServiceProvider serviceProvider,
    ILogger<ClippyService> logger)
{
    public string Provider => config["Clippy:Provider"] ?? "OpenAI";
    public string ModelName => config[$"Clippy:{Provider}:Model"] ?? "?";

    public bool IsConfigured => Provider.ToLowerInvariant() switch
    {
        "ollama" => true,
        _ => !string.IsNullOrEmpty(config[$"Clippy:{Provider}:ApiKey"])
    };

    // ---------- Sessions ----------
    public async Task<List<ChatSession>> GetSessionsAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ChatSessions.Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAt).ToListAsync();
    }

    public async Task<ChatSession> CreateSessionAsync(string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var session = new ChatSession { UserId = userId };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    public async Task DeleteSessionAsync(int sessionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var session = await db.ChatSessions.FindAsync(sessionId);
        if (session is null) return;
        db.ChatSessions.Remove(session);
        await db.SaveChangesAsync();
    }

    /// <summary>Reset: clear all messages but keep the session.</summary>
    public async Task ResetSessionAsync(int sessionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.ChatMessages.Where(m => m.SessionId == sessionId).ExecuteDeleteAsync();
        var session = await db.ChatSessions.FindAsync(sessionId);
        if (session is not null) { session.Title = "Percakapan baru"; session.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync();
    }

    public async Task<List<ChatMessageEntity>> GetMessagesAsync(int sessionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ChatMessages.Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Id).ToListAsync();
    }

    // ---------- Attachments ----------
    public async Task<ChatAttachment> UploadAttachmentAsync(string fileName, Stream content, string contentType)
    {
        var key = $"chat/{Guid.NewGuid():N}/{fileName}";
        await storage.SaveAsync(key, content, contentType);
        return new ChatAttachment
        {
            Name = fileName,
            ContentType = contentType,
            Url = $"/api/files/{key}" // served by minimal API; absolute URL composed client-side
        };
    }

    // ---------- Chat ----------
    public async IAsyncEnumerable<string> SendStreamingAsync(
        int sessionId, string userText, List<ChatAttachment> attachments,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // persist user message
        await using (var db = await dbFactory.CreateDbContextAsync())
        {
            db.ChatMessages.Add(new ChatMessageEntity
            {
                SessionId = sessionId, Role = "user", Content = userText,
                AttachmentsJson = JsonSerializer.Serialize(attachments)
            });
            var session = await db.ChatSessions.FindAsync(sessionId);
            if (session is not null)
            {
                if (session.Title == "Percakapan baru" && !string.IsNullOrWhiteSpace(userText))
                    session.Title = userText.Length > 60 ? userText[..60] + "…" : userText;
                session.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
        }

        var kernel = BuildKernel();
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = await BuildHistoryAsync(sessionId);
        var settings = BuildSettings();

        var full = new System.Text.StringBuilder();
        var stream = chat.GetStreamingChatMessageContentsAsync(history, settings, kernel, ct);

        await using var enumerator = stream.GetAsyncEnumerator(ct);
        while (true)
        {
            string? chunk = null;
            string? error = null;
            try
            {
                if (!await enumerator.MoveNextAsync()) break;
                chunk = enumerator.Current?.Content;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Clippy streaming error ({Provider})", Provider);
                error = $"\n\n⚠️ Gagal menghubungi model AI ({Provider}): {ex.Message}";
            }
            if (error is not null)
            {
                full.Append(error);
                yield return error;
                break;
            }
            if (!string.IsNullOrEmpty(chunk))
            {
                full.Append(chunk);
                yield return chunk;
            }
        }

        await PersistAssistantAsync(sessionId, full.ToString());
    }

    private async Task PersistAssistantAsync(int sessionId, string content)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        db.ChatMessages.Add(new ChatMessageEntity { SessionId = sessionId, Role = "assistant", Content = content });
        var session = await db.ChatSessions.FindAsync(sessionId);
        if (session is not null) session.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    private async Task<ChatHistory> BuildHistoryAsync(int sessionId)
    {
        var history = new ChatHistory(config["Clippy:SystemPrompt"] ??
            "You are Clippy, a helpful assistant for the BlazePoint portal.");

        var messages = await GetMessagesAsync(sessionId);
        // keep the last 30 messages to bound the context window
        foreach (var m in messages.TakeLast(30))
        {
            if (m.Role == "assistant")
            {
                history.AddAssistantMessage(m.Content);
                continue;
            }

            var attachments = ParseAttachments(m.AttachmentsJson);
            var items = new ChatMessageContentItemCollection();
            var text = m.Content;
            foreach (var att in attachments.Where(a => !a.IsImage))
                text += $"\n\n[File terlampir: {att.Name} — {att.Url}]";
            items.Add(new TextContent(text));
            foreach (var att in attachments.Where(a => a.IsImage))
            {
                // load bytes from storage so any provider (incl. local Ollama) can consume the image
                var imageBytes = await LoadAttachmentBytesAsync(att);
                if (imageBytes is not null)
                    items.Add(new ImageContent(imageBytes, att.ContentType));
            }
            history.AddUserMessage(items);
        }
        return history;
    }

    private async Task<byte[]?> LoadAttachmentBytesAsync(ChatAttachment att)
    {
        try
        {
            var key = att.Url.Replace("/api/files/", "");
            await using var stream = await storage.OpenReadAsync(Uri.UnescapeDataString(key));
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch { return null; }
    }

    public static List<ChatAttachment> ParseAttachments(string json)
    {
        try { return JsonSerializer.Deserialize<List<ChatAttachment>>(json) ?? []; }
        catch { return []; }
    }

    // ---------- Kernel factory ----------
    public Kernel BuildKernel()
    {
        var builder = Kernel.CreateBuilder();
        var provider = Provider.ToLowerInvariant();

        switch (provider)
        {
            case "anthropic":
            {
                // Anthropic exposes an OpenAI-compatible endpoint; reuse the OpenAI connector
                var client = new OpenAI.OpenAIClient(
                    new System.ClientModel.ApiKeyCredential(config["Clippy:Anthropic:ApiKey"] ?? "-"),
                    new OpenAI.OpenAIClientOptions
                    {
                        Endpoint = new Uri(config["Clippy:Anthropic:Endpoint"] ?? "https://api.anthropic.com/v1")
                    });
                builder.AddOpenAIChatCompletion(config["Clippy:Anthropic:Model"] ?? "claude-sonnet-5", client);
                break;
            }
            case "gemini":
                builder.AddGoogleAIGeminiChatCompletion(
                    config["Clippy:Gemini:Model"] ?? "gemini-2.0-flash",
                    config["Clippy:Gemini:ApiKey"] ?? "-");
                break;
            case "ollama":
#pragma warning disable SKEXP0070
                builder.AddOllamaChatCompletion(
                    config["Clippy:Ollama:Model"] ?? "llama3.2",
                    new Uri(config["Clippy:Ollama:Endpoint"] ?? "http://localhost:11434"));
#pragma warning restore SKEXP0070
                break;
            default: // OpenAI
            {
                var endpoint = config["Clippy:OpenAI:Endpoint"];
                if (!string.IsNullOrEmpty(endpoint))
                {
                    var client = new OpenAI.OpenAIClient(
                        new System.ClientModel.ApiKeyCredential(config["Clippy:OpenAI:ApiKey"] ?? "-"),
                        new OpenAI.OpenAIClientOptions { Endpoint = new Uri(endpoint) });
                    builder.AddOpenAIChatCompletion(config["Clippy:OpenAI:Model"] ?? "gpt-4o-mini", client);
                }
                else
                {
                    builder.AddOpenAIChatCompletion(
                        config["Clippy:OpenAI:Model"] ?? "gpt-4o-mini",
                        config["Clippy:OpenAI:ApiKey"] ?? "-");
                }
                break;
            }
        }

        var kernel = builder.Build();
        kernel.Plugins.AddFromObject(new UtilityPlugin(), "utility");
        kernel.Plugins.AddFromObject(new WebPlugin(httpFactory, config), "web");
        kernel.Plugins.AddFromObject(
            new BlazePointDataPlugin(serviceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>()),
            "blazepoint");
        return kernel;
    }

    private PromptExecutionSettings BuildSettings()
    {
        var temperature = double.TryParse(config["Clippy:Temperature"], out var t) ? t : 0.7;
        var maxTokens = int.TryParse(config["Clippy:MaxTokens"], out var m) ? m : 2048;

        return Provider.ToLowerInvariant() switch
        {
            "gemini" => new GeminiPromptExecutionSettings
            {
                Temperature = temperature,
                MaxTokens = maxTokens,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            },
            "ollama" =>
#pragma warning disable SKEXP0070
                new OllamaPromptExecutionSettings
                {
                    Temperature = (float)temperature,
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                },
#pragma warning restore SKEXP0070
            _ => new OpenAIPromptExecutionSettings
            {
                Temperature = temperature,
                MaxTokens = maxTokens,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            }
        };
    }
}

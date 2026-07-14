using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using BlazorViz.Data;
using BlazorViz.Services.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace BlazorViz.Services.Ai;

public sealed record ChatAttachment(string Name, string Url, string Kind, string? LocalPath);

/// <summary>
/// Data Wizard chat: builds history from the session, injects RAG context, attaches images
/// as image content and documents as links, streams the reply and records token usage.
/// </summary>
public sealed class ChatService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    AiKernelFactory kernelFactory,
    RagService rag,
    UsageService usage,
    ILogger<ChatService> log)
{
    public AiOptions Options => kernelFactory.Options;

    public async Task<ChatSession> GetOrCreateSessionAsync(int? sessionId, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (sessionId is int id)
        {
            var existing = await db.ChatSessions.Include(s => s.Messages.OrderBy(m => m.Id)).FirstOrDefaultAsync(s => s.Id == id);
            if (existing is not null) return existing;
        }
        var session = new ChatSession { UserId = userId };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    public async Task<List<ChatSession>> ListSessionsAsync(string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ChatSessions.Where(s => s.UserId == userId)
            .OrderByDescending(s => s.Id).Take(50).ToListAsync();
    }

    public async Task DeleteSessionAsync(int sessionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.ChatMessages.Where(m => m.SessionId == sessionId).ExecuteDeleteAsync();
        await db.ChatSessions.Where(s => s.Id == sessionId).ExecuteDeleteAsync();
    }

    public async Task<List<ChatMessageEntity>> ListMessagesAsync(int sessionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ChatMessages.Where(m => m.SessionId == sessionId).OrderBy(m => m.Id).ToListAsync();
    }

    /// <summary>Clears a session's history (keeps the session itself) so the user can start over.</summary>
    public async Task ResetSessionAsync(int sessionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.ChatMessages.Where(m => m.SessionId == sessionId).ExecuteDeleteAsync();
        var session = await db.ChatSessions.FindAsync(sessionId);
        if (session is not null)
        {
            session.Title = "New chat";
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Streams assistant reply chunks. Persists both user and assistant messages.</summary>
    public async IAsyncEnumerable<string> SendAsync(
        int sessionId,
        string userMessage,
        IReadOnlyList<ChatAttachment> attachments,
        string? userName,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var opts = kernelFactory.Options;

        // persist user message
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            db.ChatMessages.Add(new ChatMessageEntity
            {
                SessionId = sessionId,
                Role = "user",
                Content = userMessage,
                AttachmentsJson = attachments.Count > 0 ? JsonSerializer.Serialize(attachments) : null,
                TokensIn = Estimate(userMessage)
            });
            var session = await db.ChatSessions.FindAsync([sessionId], ct);
            if (session is not null && session.Title == "New chat")
                session.Title = userMessage.Length > 48 ? userMessage[..48] + "…" : userMessage;
            await db.SaveChangesAsync(ct);
        }

        var kernel = kernelFactory.CreateKernel();
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory(opts.SystemPrompt +
            $"\nYour name is {opts.AssistantName}. Today is {DateTime.Now:yyyy-MM-dd}.");

        var ragContext = await rag.BuildContextAsync(userMessage, ct);
        if (ragContext is not null)
            history.AddSystemMessage(ragContext);

        // replay prior turns
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            var prior = await db.ChatMessages.Where(m => m.SessionId == sessionId)
                .OrderBy(m => m.Id).ToListAsync(ct);
            foreach (var m in prior.SkipLast(1))
                history.AddMessage(m.Role == "assistant" ? AuthorRole.Assistant : AuthorRole.User, m.Content);
        }

        // current user turn with attachments
        var items = new ChatMessageContentItemCollection();
        var textContent = new StringBuilder(userMessage);
        foreach (var att in attachments)
        {
            if (att.Kind == "image" && att.LocalPath is not null && File.Exists(att.LocalPath))
            {
                var bytes = await File.ReadAllBytesAsync(att.LocalPath, ct);
                items.Add(new ImageContent(bytes, MimeOf(att.Name)));
            }
            else
            {
                textContent.Append($"\n\n[Attached document: {att.Name}]({att.Url})");
                if (att.LocalPath is not null && File.Exists(att.LocalPath) &&
                    DocumentTextExtractor.SupportedExtensions.Contains(Path.GetExtension(att.Name).ToLowerInvariant()))
                {
                    try
                    {
                        var text = DocumentTextExtractor.Extract(att.LocalPath);
                        textContent.Append($"\nDocument content (truncated):\n{(text.Length > 6000 ? text[..6000] + "…" : text)}");
                    }
                    catch { /* attachment preview is best-effort */ }
                }
            }
        }
        items.Insert(0, new Microsoft.SemanticKernel.TextContent(textContent.ToString()));
        history.AddMessage(AuthorRole.User, items);

        var settings = kernelFactory.CreateSettings();
        var reply = new StringBuilder();
        var stream = chat.GetStreamingChatMessageContentsAsync(history, settings, kernel, ct);

        await foreach (var chunk in stream)
        {
            if (string.IsNullOrEmpty(chunk.Content)) continue;
            reply.Append(chunk.Content);
            yield return chunk.Content;
        }

        var replyText = reply.ToString();
        var tokensIn = Estimate(userMessage) + (ragContext is null ? 0 : Estimate(ragContext));
        var tokensOut = Estimate(replyText);
        await using (var db = await dbFactory.CreateDbContextAsync(CancellationToken.None))
        {
            db.ChatMessages.Add(new ChatMessageEntity
            {
                SessionId = sessionId,
                Role = "assistant",
                Content = replyText,
                TokensOut = tokensOut
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        usage.Record("chat", 1, userName, opts.Provider);
        usage.Record("token_in", tokensIn, userName, opts.Active.Model);
        usage.Record("token_out", tokensOut, userName, opts.Active.Model);
        log.LogInformation("Chat turn done: session {Session}, ~{In}/{Out} tokens", sessionId, tokensIn, tokensOut);
    }

    /// <summary>Rough token estimate (~4 chars/token) used when the provider does not report usage.</summary>
    private static int Estimate(string text) => Math.Max(1, text.Length / 4);

    private static string MimeOf(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}

using AppBender.Core.Data;
using AppBender.Core.Models;
using AppBender.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AppBender.Core.AI;

public interface IChatService
{
    Task<List<ChatSession>> GetSessionsAsync();
    Task<ChatSession> CreateSessionAsync(string? title = null);
    Task DeleteSessionAsync(string sessionId);
    Task ResetSessionAsync(string sessionId);
    Task<List<ChatMessage>> GetMessagesAsync(string sessionId);
    Task UpdateSessionAsync(ChatSession session);

    /// <summary>Sends a user message and streams the assistant's reply; persists both.</summary>
    IAsyncEnumerable<string> SendAsync(string sessionId, string userText,
        List<ChatAttachment>? attachments = null, CancellationToken ct = default);

    /// <summary>Sample prompts shown on an empty chat (create form / workflow / dataset, ...).</summary>
    IReadOnlyList<(string Title, string Prompt)> SamplePrompts { get; }
}

public class ChatService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ITenantContext tenant,
    ILlmClient llm,
    IConfiguration config) : IChatService
{
    private AiOptions Options => config.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();

    public IReadOnlyList<(string Title, string Prompt)> SamplePrompts { get; } =
    [
        ("Buat form", "Buatkan saya form pendaftaran pelanggan dengan field nama, email, telepon, alamat, dan tanggal lahir."),
        ("Buat workflow", "Buatkan workflow yang mengirim email selamat datang setiap kali ada record baru di entity customers."),
        ("Buat dataset", "Rancang skema dataset untuk sistem inventaris sederhana: produk, kategori, stok masuk/keluar."),
        ("Analisis data", "Tampilkan 10 data terbaru dari dataset customers lalu ringkas pola yang menarik."),
        ("Hitung", "Berapa hasil dari round(1250000 * 1.11, 0)? Gunakan kalkulator."),
        ("Cari di internet", "Cari berita terbaru tentang perkembangan low-code platform, lalu ringkas dalam tabel."),
    ];

    public async Task<List<ChatSession>> GetSessionsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ChatSessions.AsNoTracking()
            .Where(s => s.TenantId == tenant.TenantId && s.UserId == (tenant.UserId ?? ""))
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();
    }

    public async Task<ChatSession> CreateSessionAsync(string? title = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var session = new ChatSession
        {
            TenantId = tenant.TenantId,
            UserId = tenant.UserId ?? "",
            Title = title ?? "New chat"
        };
        db.ChatSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.ChatMessages.Where(m => m.SessionId == sessionId).ExecuteDeleteAsync();
        await db.ChatSessions.Where(s => s.Id == sessionId && s.TenantId == tenant.TenantId).ExecuteDeleteAsync();
    }

    public async Task ResetSessionAsync(string sessionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.ChatMessages.Where(m => m.SessionId == sessionId).ExecuteDeleteAsync();
    }

    public async Task<List<ChatMessage>> GetMessagesAsync(string sessionId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ChatMessages.AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task UpdateSessionAsync(ChatSession session)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.ChatSessions.FirstOrDefaultAsync(s => s.Id == session.Id && s.TenantId == tenant.TenantId);
        if (existing is null) return;
        existing.Title = session.Title;
        existing.Provider = session.Provider;
        existing.Model = session.Model;
        existing.SystemPromptOverride = session.SystemPromptOverride;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async IAsyncEnumerable<string> SendAsync(string sessionId, string userText,
        List<ChatAttachment>? attachments = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ChatSession session;
        List<ChatMessage> history;
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            session = await db.ChatSessions.FirstOrDefaultAsync(
                s => s.Id == sessionId && s.TenantId == tenant.TenantId, ct)
                ?? throw new KeyNotFoundException("Chat session not found.");
            history = await db.ChatMessages.AsNoTracking()
                .Where(m => m.SessionId == sessionId).OrderBy(m => m.CreatedAt).ToListAsync(ct);

            // persist the user message (documents become markdown links appended to the text)
            var storedText = userText;
            foreach (var doc in (attachments ?? []).Where(a => !a.IsImage))
                storedText += $"\n\n📎 [{doc.FileName}]({doc.Url})";
            var userMessage = new ChatMessage
            {
                SessionId = sessionId,
                Role = "user",
                Content = storedText,
                Attachments = attachments ?? []
            };
            db.ChatMessages.Add(userMessage);
            if (session.Title == "New chat" && !string.IsNullOrWhiteSpace(userText))
                session.Title = userText.Length > 60 ? userText[..60] + "…" : userText;
            session.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        // build LLM conversation
        var options = Options;
        var messages = new List<LlmMessage>
        {
            new("system", session.SystemPromptOverride ?? options.SystemPrompt)
        };
        foreach (var m in history.Where(m => m.Role is "user" or "assistant"))
            messages.Add(new LlmMessage(m.Role, m.Content,
                m.Attachments.Where(a => a.IsImage).Select(a => a.Url).ToList() is { Count: > 0 } imgs ? imgs : null));

        var currentImages = (attachments ?? []).Where(a => a.IsImage).Select(a => a.Url).ToList();
        var currentText = userText;
        foreach (var doc in (attachments ?? []).Where(a => !a.IsImage))
            currentText += $"\n\n(Attached document: {doc.FileName} — {doc.Url})";
        messages.Add(new LlmMessage("user", currentText, currentImages.Count > 0 ? currentImages : null));

        var requestOptions = new LlmRequestOptions
        {
            Provider = session.Provider,
            Model = session.Model,
            UseTools = true
        };

        var buffer = new System.Text.StringBuilder();
        await foreach (var piece in llm.StreamAsync(messages, requestOptions, ct))
        {
            buffer.Append(piece);
            yield return piece;
        }

        await using (var db = await dbFactory.CreateDbContextAsync(CancellationToken.None))
        {
            db.ChatMessages.Add(new ChatMessage
            {
                SessionId = sessionId,
                Role = "assistant",
                Content = buffer.ToString(),
                TokensIn = Math.Max(1, messages.Sum(m => m.Content.Length) / 4),
                TokensOut = Math.Max(1, buffer.Length / 4)
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }
}

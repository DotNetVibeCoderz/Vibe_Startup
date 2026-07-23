#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0050

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using VibeWallet.Data;
using VibeWallet.Models;
using Markdig;

namespace VibeWallet.Services;

public class ChatService : IChatService
{
    private readonly VibeWalletDbContext _db;
    private readonly ChatBotConfig _cfg;
    private readonly IWalletService _wallet;
    private readonly IStorageService _storage;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<ChatService> _log;
    private readonly IServiceProvider _sp;

    public ChatService(VibeWalletDbContext db, IOptions<ChatBotConfig> cfg,
        IWalletService wallet, IStorageService storage, IHttpClientFactory http,
        ILogger<ChatService> log, IServiceProvider sp)
    { _db = db; _cfg = cfg.Value; _wallet = wallet; _storage = storage; _http = http; _log = log; _sp = sp; }

    // ===== SESSION =====

    public async Task<ChatSession> CreateSessionAsync(Guid uid, string? title = null, ChatProvider prov = ChatProvider.OpenAI)
    {
        var s = new ChatSession { UserId = uid, Title = title ?? "New Chat", Provider = prov, ModelId = ModelId(prov), Temperature = _cfg.Temperature, SystemPrompt = _cfg.SystemPrompt, IsActive = true };
        _db.ChatSessions.Add(s); await _db.SaveChangesAsync(); return s;
    }
    public async Task<ChatSession?> GetSessionAsync(Guid id) => await _db.ChatSessions.Include(s => s.Messages).ThenInclude(m => m.Attachments).FirstOrDefaultAsync(s => s.Id == id);
    public async Task<List<ChatSession>> GetUserSessionsAsync(Guid uid) => await _db.ChatSessions.Where(s => s.UserId == uid && !s.IsDeleted).OrderByDescending(s => s.LastMessageAt).ToListAsync();
    public async Task<bool> DeleteSessionAsync(Guid id) { var s = await _db.ChatSessions.FindAsync(id); if (s == null) return false; s.IsDeleted = true; s.DeletedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); return true; }
    public async Task<bool> ResetSessionAsync(Guid id) { var s = await _db.ChatSessions.Include(x => x.Messages).FirstOrDefaultAsync(x => x.Id == id); if (s == null) return false; _db.ChatMessages.RemoveRange(s.Messages); s.MessageCount = 0; s.LastMessageAt = null; await _db.SaveChangesAsync(); return true; }

    // ===== SEND MESSAGE =====

    public async Task<ChatMessage> SendMessageAsync(Guid sid, string msg, List<ChatAttachment>? atts = null)
    {
        var sess = await GetSessionAsync(sid) ?? throw new InvalidOperationException("Session not found");
        var um = new ChatMessage { ChatSessionId = sid, Role = "user", Content = msg, CreatedAt = DateTime.UtcNow };
        if (atts != null) foreach (var a in atts) { a.ChatMessageId = um.Id; um.Attachments.Add(a); }
        _db.ChatMessages.Add(um);

        string resp;
        try { resp = await KernelResponseAsync(sess, msg); }
        catch { resp = await FallbackAsync(sess.UserId, msg); } // SELALU fallback kalau error

        var html = await RenderMarkdownAsync(resp);
        var am = new ChatMessage { ChatSessionId = sid, Role = "assistant", Content = resp, RenderedContent = html, CreatedAt = DateTime.UtcNow };
        _db.ChatMessages.Add(am);
        sess.MessageCount += 2; sess.LastMessageAt = DateTime.UtcNow;
        if (sess.MessageCount <= 4 && sess.Title == "New Chat") sess.Title = msg.Length > 50 ? msg[..50] + "..." : msg;
        await _db.SaveChangesAsync();
        return am;
    }

    public async Task<List<ChatMessage>> GetSessionMessagesAsync(Guid sid) =>
        await _db.ChatMessages.Include(m => m.Attachments).Where(m => m.ChatSessionId == sid && !m.IsDeleted).OrderBy(m => m.CreatedAt).ToListAsync();
    public async Task<ChatAttachment> AddAttachmentAsync(Guid mid, AttachmentType t, string fn, string fu, string ct, long fs) { var a = new ChatAttachment { ChatMessageId = mid, Type = t, FileName = fn, FileUrl = fu, ContentType = ct, FileSize = fs }; _db.ChatAttachments.Add(a); await _db.SaveChangesAsync(); return a; }

    // ===================================================================
    //  KERNEL + PLUGINS
    // ===================================================================

    private async Task<string> KernelResponseAsync(ChatSession sess, string userMsg)
    {
        var key = KeyFor(sess.Provider);

        // 🔑 Periksa: kalau key kosong/null/whitespace/placeholder → langsung fallback
        if (string.IsNullOrWhiteSpace(key) || key.StartsWith("sk-your-") || key.StartsWith("your-") || key == "not-needed")
        {
            if (sess.Provider != ChatProvider.Ollama)
            {
                _log.LogWarning("No valid API key for {Provider}, using fallback mode", sess.Provider);
                return await FallbackAsync(sess.UserId, userMsg);
            }
        }

        // --- Build Kernel ---
        var b = Kernel.CreateBuilder();
        var ep = EndpointFor(sess.Provider);
        if (!string.IsNullOrEmpty(ep))
            b.AddOpenAIChatCompletion(sess.ModelId, endpoint: new Uri(ep), key ?? "none");//, );
        else
            b.AddOpenAIChatCompletion(sess.ModelId, key!);

        // --- Register Plugins ---
        var core = new VibeWalletCorePlugin(_db, _wallet, _sp.GetRequiredService<IRewardsService>(), _sp.GetRequiredService<ITransactionService>(), _sp.GetRequiredService<ILogger<VibeWalletCorePlugin>>());
        core.SetUserContext(sess.UserId);
        b.Plugins.AddFromObject(core, "VibeWalletCore");
        b.Plugins.AddFromObject(new VibeWalletInfoPlugin(_db, _sp.GetRequiredService<ILogger<VibeWalletInfoPlugin>>()), "VibeWalletInfo");
        b.Plugins.AddFromObject(new CommonToolsPlugin(_http, Options.Create(_cfg), _sp.GetRequiredService<ILogger<CommonToolsPlugin>>()), "CommonTools");
        var k = b.Build();

        // --- Chat History ---
        var ch = new ChatHistory(SystemPrompt(sess));
        var prev = await _db.ChatMessages.Where(m => m.ChatSessionId == sess.Id && !m.IsDeleted).OrderByDescending(m => m.CreatedAt).Take(10).OrderBy(m => m.CreatedAt).ToListAsync();
        foreach (var m in prev) { if (m.Role == "user") ch.AddUserMessage(m.Content); else if (m.Role == "assistant") ch.AddAssistantMessage(m.Content); }
        ch.AddUserMessage(userMsg);

        // --- Execute ---
        var cc = k.GetRequiredService<IChatCompletionService>();
        var set = new OpenAIPromptExecutionSettings
        {
            Temperature = (double)sess.Temperature, MaxTokens = _cfg.MaxTokens, TopP = (double)_cfg.TopP,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true)
        };

        // ⚠️ TRY-CATCH di dalam kernel call: kalau API error → fallback
        try
        {
            var r = await cc.GetChatMessageContentAsync(ch, set, k);
            return r.Content ?? "Maaf, tidak bisa merespon.";
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Kernel API call failed, falling back to local responses");
            return await FallbackAsync(sess.UserId, userMsg);
        }
    }

    private string SystemPrompt(ChatSession sess)
    {
        var ctx = UserCtx(sess.UserId).GetAwaiter().GetResult();
        return $@"{sess.SystemPrompt ?? _cfg.SystemPrompt}

{ctx}

FUNGSI YANG BISA DIPANGGIL (jangan mengarang data!):
VibeWalletCore.get_user_balance | get_transaction_history | get_user_profile | get_daily_limits
VibeWalletInfo.get_active_promos | get_available_vouchers | get_supported_banks | get_insurance_products | get_savings_info
CommonTools.search_internet | scrap_web_page | get_current_datetime | calculate_math | get_exchange_rate | read_file_from_url";
    }

    private async Task<string> UserCtx(Guid uid)
    {
        try { var w = await _wallet.GetWalletByUserIdAsync(uid); var u = await _db.Users.FindAsync(uid); if (w == null || u == null) return ""; return $"PENGGUNA: {u.FullName} | Saldo: Rp {w.AvailableBalance:N0} | Wallet: {w.WalletNumber} | Points: {w.LoyaltyPoints} | KYC: {u.KycStatus}"; }
        catch { return ""; }
    }

    // ===== FALLBACK (lengkap + smart) =====

    private async Task<string> FallbackAsync(Guid uid, string msg)
    {
        var l = msg.ToLower();

        // Saldo / Balance
        if (l.Contains("saldo") || l.Contains("balance") || l.Contains("cek") && l.Contains("uang"))
        { var b = await _wallet.GetBalanceAsync(uid); return $"💰 Saldo kakak saat ini **Rp {b:N0}**. Mau top-up atau ada yang bisa Mbak Selvi bantu lagi? 😊"; }

        // Transaksi / Mutasi
        if (l.Contains("transaksi") || l.Contains("mutasi") || l.Contains("riwayat"))
        { var tx = await _db.WalletTransactions.Where(t => t.UserId == uid && !t.IsDeleted).OrderByDescending(t => t.CreatedAt).Take(5).ToListAsync(); return tx.Any() ? "📋 **Transaksi Terakhir:**\n\n" + string.Join("\n", tx.Select(t => $"• {t.CreatedAt:dd/MM HH:mm} — {t.Description} — Rp {t.Amount:N0} ({t.Status})")) : "Belum ada transaksi nih kak."; }

        // Promo
        if (l.Contains("promo") || l.Contains("diskon") || l.Contains("cashback"))
        { var pp = await _db.Promos.Where(p => p.IsActive && p.ValidFrom <= DateTime.UtcNow && p.ValidUntil >= DateTime.UtcNow).Take(5).ToListAsync(); return pp.Any() ? "🎉 **Promo Aktif:**\n\n" + string.Join("\n", pp.Select((p, i) => $"{i + 1}. **{p.Title}** — {p.Description} (s/d {p.ValidUntil:dd MMM})")) : "Belum ada promo spesial nih kak. Cek lagi nanti ya! 😊"; }

        // Transfer
        if (l.Contains("transfer") || l.Contains("kirim") && (l.Contains("uang") || l.Contains("duit")))
            return "💸 **Transfer di VibeWallet:**\n\n• Ke sesama VibeWallet: **GRATIS!** 🎉\n• Ke rekening bank: biaya admin **Rp 2.500**\n• Minimal transfer: **Rp 10.000**\n• Maksimal per hari: **Rp 25.000.000**\n\nCaranya: klik menu Transfer di sidebar ya kak!";

        // Top-up
        if (l.Contains("topup") || l.Contains("top up") || l.Contains("isi saldo") || l.Contains("deposit"))
            return "📥 **Cara Top-Up VibeWallet:**\n\n🏦 **Transfer Bank** — BCA, Mandiri, BNI, BRI, CIMB Niaga, dll\n🏪 **Gerai Retail** — Alfamart, Indomaret\n💳 **Virtual Account**\n\nMinimal top-up: **Rp 5.000**\nMaksimal saldo: **Rp 50.000.000**\n\nMau top-up sekarang? Klik menu Top Up ya!";

        // Bank
        if (l.Contains("bank") || l.Contains("rekening"))
        { var banks = await _db.SupportedBanks.Where(b => b.IsActive).OrderBy(b => b.SortOrder).Take(8).ToListAsync(); return "🏦 **Bank Didukung:**\n\n" + string.Join("\n", banks.Select(b => $"• **{b.BankName}** (Kode: {b.BankCode}) — Admin: Rp {b.AdminFee:N0}")); }

        // Tagihan / Bills
        if (l.Contains("tagihan") || l.Contains("listrik") || l.Contains("pln") || l.Contains("pdam") || l.Contains("bpjs") || l.Contains("internet"))
            return "📋 **Bayar Tagihan di VibeWallet:**\n\n✅ Listrik (PLN)\n✅ Air (PDAM)\n✅ BPJS Kesehatan\n✅ Internet & TV Kabel\n✅ Pulsa & Paket Data\n\nCaranya: menu Bills di sidebar ya kak! Biaya admin mulai Rp 2.500 saja.";

        // Pulsa
        if (l.Contains("pulsa") || l.Contains("paket data") || l.Contains("kuota"))
            return "📶 **Isi Pulsa & Paket Data:**\n\nProvider: Telkomsel, Indosat, XL, Tri, Smartfren, Axis\n\nPulsa mulai dari **Rp 5.000**\nPaket data mulai dari **Rp 15.000** (1GB)\n\nBisa juga isi token listrik PLN! ⚡";

        // Tabungan / Savings
        if (l.Contains("tabungan") || l.Contains("saving") || l.Contains("bunga") || l.Contains("deposito"))
            return "🏦 **Tabungan Digital VibeWallet:**\n\n💰 Bunga **3.5% per tahun**\n📅 Bunga dihitung bulanan\n💸 Setoran minimal **Rp 10.000**\n🆓 Bebas biaya admin!\n\nMau buka tabungan? Klik menu Savings ya!";

        // Asuransi
        if (l.Contains("asuransi") || l.Contains("insurance") || l.Contains("proteksi"))
            return "🛡️ **Produk Asuransi VibeWallet:**\n\n🏥 Kesehatan — mulai Rp 150.000/bulan\n✈️ Perjalanan — mulai Rp 25.000\n📱 Gadget — mulai Rp 50.000/bulan\n👨‍👩‍👧‍👦 Jiwa — mulai Rp 200.000/bulan\n\nLihat detail di menu Savings → Insurance ya kak!";

        // KYC
        if (l.Contains("kyc") || l.Contains("verifikasi") || l.Contains("ktp"))
            return "📄 **Verifikasi KYC:**\n\nUntuk fitur lengkap VibeWallet, kakak perlu verifikasi identitas:\n1. Upload foto KTP/SIM/Paspor\n2. Upload foto selfie\n3. Tunggu verifikasi (1x24 jam)\n\nStatus KYC bisa dicek di halaman Settings ya!";

        // Bantuan / Help
        if (l.Contains("bantu") || l.Contains("help") || l.Contains("cs") || l.Contains("customer") || l.Contains("komplain"))
            return "📞 **Butuh bantuan?**\n\n📧 Email: support@vibewallet.id\n📱 Phone: +62812-3456-7890\n💬 Chat ini juga bisa bantu kok!\n\nJam operasional: Senin-Minggu, 07:00-22:00 WIB.";

        // Sapaan / Greeting
        if (l.Contains("halo") || l.Contains("hai") || l.Contains("hi") || l.Contains("pagi") || l.Contains("siang") || l.Contains("malam") || l.Contains("selamat"))
        {
            var jam = DateTime.Now.Hour;
            var sapaan = jam < 11 ? "pagi" : jam < 15 ? "siang" : jam < 19 ? "sore" : "malam";
            return $"Halo kak! Selamat {sapaan}! 👋 Mbak Selvi di sini, asisten virtual VibeWallet. Ada yang bisa dibantu? 😊";
        }

        return new[] {
            "Halo kak! 👋 Mbak Selvi siap membantu. Coba ketik: cek saldo, promo, transfer, topup, atau tagihan ya!",
            "Mbak Selvi di sini kak! 😊 Mau tanya soal saldo, promo, transfer, atau topup?",
            "Ada yang bisa Mbak Selvi bantu? Ketik aja: cek saldo, lihat promo, cara transfer, dll 🙌",
            "Hai bestie VibeWallet! 💜 Coba ketik 'cek saldo', 'promo', atau 'transfer' ya~"
        }[Random.Shared.Next(4)];
    }

    // ===== HELPERS =====

    private string? KeyFor(ChatProvider p) => p switch { ChatProvider.OpenAI => _cfg.Models?.OpenAI?.ApiKey, ChatProvider.Anthropic => _cfg.Models?.Anthropic?.ApiKey, ChatProvider.Gemini => _cfg.Models?.Gemini?.ApiKey, _ => _cfg.Models?.OpenAI?.ApiKey };
    private string? EndpointFor(ChatProvider p) => p switch { ChatProvider.Anthropic => _cfg.Models?.Anthropic?.Endpoint, ChatProvider.Gemini => _cfg.Models?.Gemini?.Endpoint, ChatProvider.Ollama => _cfg.Models?.Ollama?.Endpoint, _ => _cfg.Models?.OpenAI?.Endpoint };
    private string ModelId(ChatProvider p) => p switch { ChatProvider.OpenAI => _cfg.Models?.OpenAI?.ModelId ?? "gpt-4o", ChatProvider.Anthropic => _cfg.Models?.Anthropic?.ModelId ?? "claude-3-5-sonnet-20241022", ChatProvider.Gemini => _cfg.Models?.Gemini?.ModelId ?? "gemini-1.5-pro", ChatProvider.Ollama => _cfg.Models?.Ollama?.ModelId ?? "llama3.2", _ => "gpt-4o" };
    public Task<List<string>> GetAvailableModelsAsync() => Task.FromResult(new List<string> { "gpt-4o (OpenAI)", "gpt-4-turbo (OpenAI)", "gpt-3.5-turbo (OpenAI)", "claude-3-5-sonnet (Anthropic)", "gemini-1.5-pro (Google)", "llama3.2 (Ollama)" });
    public async Task UpdateSessionConfigAsync(Guid id, ChatProvider p, string m, decimal t) { var s = await _db.ChatSessions.FindAsync(id); if (s == null) return; s.Provider = p; s.ModelId = m; s.Temperature = t; await _db.SaveChangesAsync(); }
    private CommonToolsPlugin Tools() => new(_http, Options.Create(_cfg), _sp.GetRequiredService<ILogger<CommonToolsPlugin>>());
    public async Task<string> SearchInternetAsync(string q) => await Tools().SearchInternet(q);
    public async Task<string> ScrapWebPageAsync(string u) => await Tools().ScrapWebPage(u);
    public async Task<string> ReadFileFromUrlAsync(string u) => await Tools().ReadFileFromUrl(u);
    public Task<string> GetCurrentDateTimeAsync(string tz = "Asia/Jakarta") => Task.FromResult(Tools().GetCurrentDateTime(tz));
    public Task<string> CalculateMathAsync(string e) => Task.FromResult(Tools().CalculateMath(e));
    public async Task<string> QueryDatabaseAsync(string ctx) { var c = new VibeWalletCorePlugin(_db, _wallet, _sp.GetRequiredService<IRewardsService>(), _sp.GetRequiredService<ITransactionService>(), _sp.GetRequiredService<ILogger<VibeWalletCorePlugin>>()); return ctx.ToLower() switch { var s when s.Contains("saldo") => await c.GetUserBalance(), var s when s.Contains("transaksi") => await c.GetTransactionHistory(), _ => "Unknown" }; }
    public async Task<string> RenderMarkdownAsync(string md) { await Task.CompletedTask; try { return Markdown.ToHtml(md, new MarkdownPipelineBuilder().UseAdvancedExtensions().UsePipeTables().UseTaskLists().UseEmojiAndSmiley().UseAutoLinks().Build()); } catch { return md.Replace("\n", "<br>"); } }
}

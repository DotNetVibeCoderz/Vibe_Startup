using BlazePoint.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlazePoint.Services.Clippy;

/// <summary>Utility functions: date/time and math.</summary>
public class UtilityPlugin
{
    [KernelFunction("get_current_datetime")]
    [Description("Mendapatkan tanggal dan waktu saat ini (server, WIB/UTC).")]
    public string GetCurrentDateTime()
    {
        var utc = DateTime.UtcNow;
        var wib = utc.AddHours(7);
        return $"UTC: {utc:dddd, dd MMMM yyyy HH:mm:ss}; WIB (UTC+7): {wib:dddd, dd MMMM yyyy HH:mm:ss}";
    }

    [KernelFunction("date_difference")]
    [Description("Menghitung selisih hari antara dua tanggal (format yyyy-MM-dd).")]
    public string DateDifference(
        [Description("Tanggal awal, format yyyy-MM-dd")] string from,
        [Description("Tanggal akhir, format yyyy-MM-dd")] string to)
    {
        if (!DateTime.TryParse(from, out var d1) || !DateTime.TryParse(to, out var d2))
            return "Format tanggal tidak valid. Gunakan yyyy-MM-dd.";
        var diff = d2 - d1;
        return $"Selisih: {diff.TotalDays:0} hari ({diff.TotalDays / 7:0.#} minggu).";
    }

    [KernelFunction("calculate")]
    [Description("Menghitung ekspresi matematika, contoh: (2+3)*4/5, sqrt tidak didukung — gunakan operasi aritmetika dasar, %, dan tanda kurung.")]
    public string Calculate([Description("Ekspresi matematika")] string expression)
    {
        try
        {
            // DataTable.Compute supports + - * / % and parentheses
            var sanitized = Regex.Replace(expression, @"[^\d+\-*/%.()\s,]", "");
            var result = new DataTable().Compute(sanitized, null);
            return $"{expression} = {result}";
        }
        catch (Exception ex)
        {
            return $"Tidak bisa menghitung '{expression}': {ex.Message}";
        }
    }

    [KernelFunction("convert_units")]
    [Description("Konversi satuan umum: km<->mile, kg<->lb, celsius<->fahrenheit.")]
    public string ConvertUnits(
        [Description("Nilai numerik")] double value,
        [Description("Satuan asal: km, mile, kg, lb, c, f")] string from,
        [Description("Satuan tujuan: km, mile, kg, lb, c, f")] string to)
    {
        var result = (from.ToLower(), to.ToLower()) switch
        {
            ("km", "mile") => value * 0.621371,
            ("mile", "km") => value / 0.621371,
            ("kg", "lb") => value * 2.20462,
            ("lb", "kg") => value / 2.20462,
            ("c", "f") => value * 9 / 5 + 32,
            ("f", "c") => (value - 32) * 5 / 9,
            _ => double.NaN
        };
        return double.IsNaN(result) ? "Konversi tidak didukung." : $"{value} {from} = {result:0.###} {to}";
    }
}

/// <summary>Web functions: Tavily search, scrape page, read file from URL.</summary>
public class WebPlugin(IHttpClientFactory httpFactory, IConfiguration config)
{
    [KernelFunction("search_internet")]
    [Description("Mencari informasi di internet menggunakan Tavily. Gunakan untuk pertanyaan tentang berita, fakta terkini, atau info di luar data internal.")]
    public async Task<string> SearchInternetAsync([Description("Kata kunci pencarian")] string query)
    {
        var apiKey = config["Clippy:Tavily:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            return "Tavily API key belum dikonfigurasi (Clippy:Tavily:ApiKey di appsettings.json).";
        var http = httpFactory.CreateClient("clippy");
        var res = await http.PostAsJsonAsync("https://api.tavily.com/search", new
        {
            api_key = apiKey, query, max_results = 5, include_answer = true
        });
        if (!res.IsSuccessStatusCode) return $"Pencarian gagal: {res.StatusCode}";
        var json = await res.Content.ReadFromJsonAsync<JsonElement>();
        var sb = new System.Text.StringBuilder();
        if (json.TryGetProperty("answer", out var answer) && answer.ValueKind == JsonValueKind.String)
            sb.AppendLine($"Ringkasan: {answer.GetString()}\n");
        if (json.TryGetProperty("results", out var results))
            foreach (var r in results.EnumerateArray())
                sb.AppendLine($"- {r.GetProperty("title").GetString()} ({r.GetProperty("url").GetString()}): {r.GetProperty("content").GetString()?[..Math.Min(300, r.GetProperty("content").GetString()!.Length)]}");
        return sb.Length > 0 ? sb.ToString() : "Tidak ada hasil.";
    }

    [KernelFunction("scrape_url")]
    [Description("Mengambil dan membaca isi teks dari sebuah halaman web (URL).")]
    public async Task<string> ScrapeUrlAsync([Description("URL halaman web")] string url)
    {
        try
        {
            var http = httpFactory.CreateClient("clippy");
            var html = await http.GetStringAsync(url);
            // strip scripts/styles/tags to plain text
            html = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var text = Regex.Replace(html, "<[^>]+>", " ");
            text = System.Net.WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"\s+", " ").Trim();
            return text.Length > 6000 ? text[..6000] + "…" : text;
        }
        catch (Exception ex)
        {
            return $"Gagal membaca {url}: {ex.Message}";
        }
    }

    [KernelFunction("read_file_from_url")]
    [Description("Membaca isi file teks (txt, md, csv, json, xml, log, kode) dari sebuah URL.")]
    public async Task<string> ReadFileFromUrlAsync([Description("URL file")] string url)
    {
        try
        {
            var http = httpFactory.CreateClient("clippy");
            using var res = await http.GetAsync(url);
            res.EnsureSuccessStatusCode();
            var contentType = res.Content.Headers.ContentType?.MediaType ?? "";
            if (contentType.StartsWith("image/") || contentType.StartsWith("video/") || contentType.StartsWith("audio/"))
                return $"File adalah {contentType} — tidak bisa dibaca sebagai teks. Ukuran: {res.Content.Headers.ContentLength} bytes.";
            var text = await res.Content.ReadAsStringAsync();
            return text.Length > 8000 ? text[..8000] + "…" : text;
        }
        catch (Exception ex)
        {
            return $"Gagal membaca file {url}: {ex.Message}";
        }
    }
}

/// <summary>Query internal BlazePoint data so Clippy can answer questions about the portal content.</summary>
public class BlazePointDataPlugin(IDbContextFactory<ApplicationDbContext> dbFactory)
{
    [KernelFunction("get_portal_statistics")]
    [Description("Statistik portal BlazePoint: jumlah dokumen, situs, pengguna, halaman, list, diskusi, event.")]
    public async Task<string> GetStatisticsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return $"""
            Statistik BlazePoint:
            - Dokumen aktif: {await db.Documents.CountAsync(d => !d.IsDeleted)}
            - Total ukuran dokumen: {(await db.Documents.Where(d => !d.IsDeleted).SumAsync(d => (long?)d.Size) ?? 0) / 1024.0 / 1024.0:0.##} MB
            - Team sites: {await db.Sites.CountAsync()}
            - Pengguna terdaftar: {await db.Users.CountAsync()}
            - Halaman CMS: {await db.CmsPages.CountAsync()} ({await db.CmsPages.CountAsync(p => p.IsPublished)} published)
            - Custom lists: {await db.Lists.CountAsync()}
            - Thread diskusi: {await db.DiscussionThreads.CountAsync()}
            - Event mendatang: {await db.CalendarEvents.CountAsync(e => e.Start >= DateTime.Now)}
            - Approval pending: {await db.ApprovalTasks.CountAsync(t => t.Status == ApprovalStatus.Pending)}
            """;
    }

    [KernelFunction("search_documents")]
    [Description("Mencari dokumen di BlazePoint berdasarkan nama.")]
    public async Task<string> SearchDocumentsAsync([Description("Kata kunci nama dokumen")] string keyword)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var docs = await db.Documents
            .Where(d => !d.IsDeleted && EF.Functions.Like(d.Name, $"%{keyword}%"))
            .OrderByDescending(d => d.UpdatedAt).Take(10).ToListAsync();
        if (docs.Count == 0) return $"Tidak ada dokumen dengan kata kunci '{keyword}'.";
        return "Dokumen ditemukan:\n" + string.Join("\n",
            docs.Select(d => $"- {d.Name} (v{d.Version}, {d.Size / 1024.0:0.#} KB, folder {d.FolderPath}, link: /documents/{d.Id})"));
    }

    [KernelFunction("list_team_sites")]
    [Description("Menampilkan daftar team sites yang ada di BlazePoint.")]
    public async Task<string> ListSitesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var sites = await db.Sites.ToListAsync();
        return sites.Count == 0 ? "Belum ada team site." :
            string.Join("\n", sites.Select(s => $"- {s.Icon} {s.Name} (/sites/{s.Slug}) — {s.Description}"));
    }

    [KernelFunction("get_upcoming_events")]
    [Description("Menampilkan event kalender yang akan datang.")]
    public async Task<string> GetUpcomingEventsAsync([Description("Jumlah maksimal event")] int count = 5)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var events = await db.CalendarEvents.Where(e => e.End >= DateTime.Now)
            .OrderBy(e => e.Start).Take(Math.Clamp(count, 1, 20)).ToListAsync();
        return events.Count == 0 ? "Tidak ada event mendatang." :
            string.Join("\n", events.Select(e =>
                $"- {e.Title}: {e.Start:dd MMM yyyy HH:mm}{(string.IsNullOrEmpty(e.Location) ? "" : $" di {e.Location}")}"));
    }

    [KernelFunction("get_recent_discussions")]
    [Description("Menampilkan diskusi terbaru di discussion board.")]
    public async Task<string> GetRecentDiscussionsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var threads = await db.DiscussionThreads.Include(t => t.Posts)
            .OrderByDescending(t => t.CreatedAt).Take(5).ToListAsync();
        return threads.Count == 0 ? "Belum ada diskusi." :
            string.Join("\n", threads.Select(t => $"- {t.Title} ({t.Posts.Count} balasan, link: /discussions/{t.Id})"));
    }

    [KernelFunction("search_lists")]
    [Description("Mencari data di custom lists BlazePoint (misal aset IT, kontak karyawan).")]
    public async Task<string> SearchListsAsync([Description("Kata kunci")] string keyword)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var items = await db.ListItems
            .Where(i => EF.Functions.Like(i.ValuesJson, $"%{keyword}%"))
            .Take(10).ToListAsync();
        if (items.Count == 0) return $"Tidak ada item list yang cocok dengan '{keyword}'.";
        var listIds = items.Select(i => i.ListId).Distinct().ToList();
        var lists = await db.Lists.Where(l => listIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Name);
        return string.Join("\n", items.Select(i => $"- [{lists.GetValueOrDefault(i.ListId, "?")}] {i.ValuesJson}"));
    }

    [KernelFunction("get_page_content")]
    [Description("Membaca konten halaman CMS yang dipublikasikan berdasarkan judul atau slug.")]
    public async Task<string> GetPageContentAsync([Description("Judul atau slug halaman")] string titleOrSlug)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var page = await db.CmsPages.FirstOrDefaultAsync(p =>
            p.Slug == titleOrSlug || EF.Functions.Like(p.Title, $"%{titleOrSlug}%"));
        if (page is null) return $"Halaman '{titleOrSlug}' tidak ditemukan.";
        var parts = PageService.ParseParts(page.PublishedJson);
        var content = string.Join("\n\n", parts.Select(p => $"[{p.Title}]\n{p.Settings.GetValueOrDefault("content", $"(webpart {p.Type})")}"));
        return $"Halaman: {page.Title} (/p/{page.Slug}, layout {page.Layout}, v{page.Version})\n\n{content}";
    }
}

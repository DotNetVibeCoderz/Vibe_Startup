using System.ComponentModel;
using System.Text.Json;
using System.Web;
using System.Text.RegularExpressions;
using Comblang.Data;
using Comblang.Models;
using Comblang.Services.Location;
using Comblang.Services.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace Comblang.Services.AI;

/// <summary>
/// Semantic Kernel plugin exposing Comblang-specific functions to the
/// Si Mak Comblang chatbot: internet search (Tavily), web scraping,
/// file reading, profile queries, compatibility calculation, and
/// current date/time.
/// </summary>
public class KernelFunctions
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public KernelFunctions(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    // -------------------------------------------------------
    //  SearchInternet (Tavily)
    // -------------------------------------------------------

    /// <summary>
    /// Searches the internet using the Tavily API and returns formatted results.
    /// </summary>
    [KernelFunction("SearchInternet")]
    [Description("Mencari informasi di internet menggunakan Tavily. " +
                 "Gunakan untuk mencari informasi terkini, berita, atau referensi.")]
    public async Task<string> SearchInternetAsync(
        [Description("Kata kunci pencarian")] string query)
    {
        var apiKey = _config["Tavily:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return "Maaf, fitur pencarian internet belum dikonfigurasi (API key Tavily belum diatur).";

        try
        {
            var requestBody = JsonSerializer.Serialize(new
            {
                query,
                api_key = apiKey,
                max_results = 5
            });

            var content = new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("https://api.tavily.com/search", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            var results = doc.RootElement.GetProperty("results");
            var formatted = new List<string>();

            foreach (var result in results.EnumerateArray().Take(5))
            {
                var title = result.GetProperty("title").GetString() ?? "";
                var url = result.GetProperty("url").GetString() ?? "";
                var snippet = result.GetProperty("content").GetString() ?? "";
                formatted.Add($"**{title}**\n{snippet}\n\ud83d\udd17 {url}");
            }

            return formatted.Count > 0
                ? string.Join("\n\n---\n\n", formatted)
                : "Tidak ditemukan hasil untuk pencarian tersebut.";
        }
        catch (Exception ex)
        {
            return $"Gagal mencari: {ex.Message}";
        }
    }

    // -------------------------------------------------------
    //  ScrapPage
    // -------------------------------------------------------

    /// <summary>
    /// Scrapes a web page and returns its plain-text content (truncated to 5000 chars).
    /// </summary>
    [KernelFunction("ScrapPage")]
    [Description("Mengambil dan membaca konten dari halaman web.")]
    public async Task<string> ScrapPageAsync(
        [Description("URL halaman web yang akan di-scrape")] string url)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(url);

            // Strip HTML tags and decode entities
            var text = HttpUtility.HtmlDecode(
                Regex.Replace(response, "<[^>]+>", " "));
            text = Regex.Replace(text, @"\s+", " ").Trim();

            if (text.Length > 5000)
                text = text[..5000] + "... (dipotong)";

            return text;
        }
        catch (Exception ex)
        {
            return $"Gagal membaca halaman: {ex.Message}";
        }
    }

    // -------------------------------------------------------
    //  ReadFileFromUrl
    // -------------------------------------------------------

    /// <summary>
    /// Reads a text-based file from a URL (.txt, .md, .json, .csv etc.).
    /// Truncates to 10 000 characters.
    /// </summary>
    [KernelFunction("ReadFileFromUrl")]
    [Description("Membaca file dokumen dari URL (mendukung .txt, .md, .json, .csv).")]
    public async Task<string> ReadFileFromUrlAsync(
        [Description("URL file yang akan dibaca")] string url)
    {
        try
        {
            var content = await _httpClient.GetStringAsync(url);

            if (content.Length > 10000)
                content = content[..10000] + "... (dipotong)";

            return content;
        }
        catch (Exception ex)
        {
            return $"Gagal membaca file: {ex.Message}";
        }
    }

    // -------------------------------------------------------
    //  QueryUserProfiles
    // -------------------------------------------------------

    /// <summary>
    /// Queries the database for user profiles matching natural-language criteria.
    /// </summary>
    [KernelFunction("QueryUserProfiles")]
    [Description("Mencari profil pengguna di database berdasarkan kriteria " +
                 "seperti gender, usia, lokasi, hobi, dll.")]
    public async Task<string> QueryUserProfilesAsync(
        [Description("Kriteria pencarian dalam bahasa natural")] string criteria)
    {
        try
        {
            var users = await _db.Users
                .Include(u => u.Profile)
                .Include(u => u.InterestTags)
                .Where(u => !u.IsBanned)
                .Take(20)
                .ToListAsync();

            if (users.Count == 0)
                return "Belum ada pengguna terdaftar di database.";

            // Simple keyword matching
            var keywords = criteria.ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var ranked = users
                .Select(u =>
                {
                    var bio = u.Profile?.Bio?.ToLower() ?? "";
                    var tags = string.Join(" ",
                        u.InterestTags.Select(t => t.TagName.ToLower()));
                    var searchText = $"{u.Username.ToLower()} {bio} {tags} " +
                                     $"{u.City?.ToLower()} {u.Profile?.Gender?.ToLower()}";

                    var hits = keywords.Count(k => searchText.Contains(k));
                    return (user: u, hits);
                })
                .OrderByDescending(x => x.hits)
                .ThenBy(x => Guid.NewGuid()) // shuffle ties
                .Take(5)
                .ToList();

            var result = ranked.Select(r =>
            {
                var u = r.user;
                var tagNames = u.InterestTags.Select(t => t.TagName);
                return $"**{u.Username}** | {u.Profile?.Gender ?? "N/A"} | " +
                       $"{u.City ?? "Unknown"}\n" +
                       $"Bio: {u.Profile?.Bio ?? "Tidak ada bio"}\n" +
                       $"Tags: {(tagNames.Any() ? string.Join(", ", tagNames) : "Tidak ada tag")}\n" +
                       $"---";
            });

            return string.Join("\n\n", result);
        }
        catch (Exception ex)
        {
            return $"Gagal query database: {ex.Message}";
        }
    }

    // -------------------------------------------------------
    //  CalculateCompatibility
    // -------------------------------------------------------

    /// <summary>
    /// Calculates a compatibility score between two users identified by
    /// username or email.
    /// </summary>
    [KernelFunction("CalculateCompatibility")]
    [Description("Menghitung skor kecocokan antara dua pengguna berdasarkan " +
                 "minat, lokasi, dan profil.")]
    public async Task<string> CalculateCompatibilityAsync(
        [Description("Username atau email pengguna pertama")] string user1Identifier,
        [Description("Username atau email pengguna kedua")] string user2Identifier)
    {
        try
        {
            var user1 = await _db.Users
                .Include(u => u.InterestTags)
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u =>
                    u.Username == user1Identifier || u.Email == user1Identifier);

            var user2 = await _db.Users
                .Include(u => u.InterestTags)
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u =>
                    u.Username == user2Identifier || u.Email == user2Identifier);

            if (user1 == null) return $"Pengguna '{user1Identifier}' tidak ditemukan.";
            if (user2 == null) return $"Pengguna '{user2Identifier}' tidak ditemukan.";

            var engine = new MatchEngine(_db);
            var score = await engine.CalculateCompatibilityAsync(user1.Id, user2.Id);

            var tags1 = user1.InterestTags.Select(t => t.TagName).ToList();
            var tags2 = user2.InterestTags.Select(t => t.TagName).ToList();
            var commonTags = tags1.Intersect(tags2).ToList();

            var distance = GeoService.CalculateDistance(
                user1.Latitude, user1.Longitude,
                user2.Latitude, user2.Longitude);

            var verdict = score switch
            {
                >= 80 => "\ud83c\udf1f Kecocokan sangat tinggi! Kalian seperti ditakdirkan!",
                >= 60 => "\ud83d\ude0a Kecocokan cukup baik, banyak kesamaan!",
                >= 40 => "\ud83e\udd14 Ada potensi, perlu eksplorasi lebih lanjut.",
                _     => "\ud83d\udcad Cukup berbeda, tapi siapa tahu bisa saling melengkapi!"
            };

            return $"""
                \ud83d\udc95 **Skor Kecocokan: {score}/100**

                \ud83d\udcca **Analisis:**
                - Minat yang sama: {(commonTags.Count > 0 ? string.Join(", ", commonTags) : "Tidak ada")}
                - Jarak: {distance:F1} km
                - Total tag {user1.Username}: {tags1.Count} | {user2.Username}: {tags2.Count}

                {verdict}
                """;
        }
        catch (Exception ex)
        {
            return $"Gagal menghitung kecocokan: {ex.Message}";
        }
    }

    // -------------------------------------------------------
    //  GetCurrentDateTime
    // -------------------------------------------------------

    /// <summary>
    /// Returns the current date and time in WIB (UTC+7).
    /// </summary>
    [KernelFunction("GetCurrentDateTime")]
    [Description("Mendapatkan waktu dan tanggal saat ini dalam zona WIB.")]
    public string GetCurrentDateTime()
    {
        var now = DateTime.UtcNow;

        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var local = TimeZoneInfo.ConvertTimeFromUtc(now, tz);
            return local.ToString("dddd, dd MMMM yyyy HH:mm 'WIB'");
        }
        catch (TimeZoneNotFoundException)
        {
            // Fallback if the Windows time-zone ID isn't available (e.g. Linux)
            var local = now.AddHours(7);
            return local.ToString("dddd, dd MMMM yyyy HH:mm 'WIB'");
        }
    }
}

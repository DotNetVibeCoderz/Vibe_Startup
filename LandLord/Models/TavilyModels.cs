using System.Text.Json.Serialization;

namespace LandLord.Models;

// ================================================================
// Tavily Search API Models
// API Docs: https://docs.tavily.com/
// ================================================================

/// <summary>
/// Request untuk Tavily Search API
/// </summary>
public class TavilySearchRequest
{
    /// <summary>Kata kunci pencarian</summary>
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    /// <summary>Kedalaman pencarian: "basic" atau "advanced"</summary>
    [JsonPropertyName("search_depth")]
    public string SearchDepth { get; set; } = "advanced";

    /// <summary>Apakah sertakan jawaban AI-generated</summary>
    [JsonPropertyName("include_answer")]
    public bool IncludeAnswer { get; set; } = true;

    /// <summary>Apakah sertakan raw content</summary>
    [JsonPropertyName("include_raw_content")]
    public bool IncludeRawContent { get; set; }

    /// <summary>Apakah sertakan gambar</summary>
    [JsonPropertyName("include_images")]
    public bool IncludeImages { get; set; }

    /// <summary>Maksimum hasil pencarian (1-20)</summary>
    [JsonPropertyName("max_results")]
    public int MaxResults { get; set; } = 5;

    /// <summary>Domain untuk di-include (opsional)</summary>
    [JsonPropertyName("include_domains")]
    public List<string>? IncludeDomains { get; set; }

    /// <summary>Domain untuk di-exclude (opsional)</summary>
    [JsonPropertyName("exclude_domains")]
    public List<string>? ExcludeDomains { get; set; }

    /// <summary>Topik: "general" atau "news"</summary>
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = "general";
}

/// <summary>
/// Response dari Tavily Search API
/// </summary>
public class TavilySearchResponse
{
    /// <summary>Query yang dicari</summary>
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    /// <summary>Waktu eksekusi dalam detik</summary>
    [JsonPropertyName("response_time")]
    public double ResponseTime { get; set; }

    /// <summary>Jawaban AI-generated (ringkasan)</summary>
    [JsonPropertyName("answer")]
    public string? Answer { get; set; }

    /// <summary>Hasil pencarian</summary>
    [JsonPropertyName("results")]
    public List<TavilySearchResult> Results { get; set; } = new();

    /// <summary>Gambar terkait (jika di-request)</summary>
    [JsonPropertyName("images")]
    public List<TavilyImage>? Images { get; set; }
}

/// <summary>
/// Satu hasil pencarian Tavily
/// </summary>
public class TavilySearchResult
{
    /// <summary>Judul halaman</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>URL halaman</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    /// <summary>Konten hasil (ringkasan)</summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>Skor relevansi (0-1)</summary>
    [JsonPropertyName("score")]
    public double Score { get; set; }

    /// <summary>Raw content (jika di-request)</summary>
    [JsonPropertyName("raw_content")]
    public string? RawContent { get; set; }
}

/// <summary>
/// Gambar dari hasil pencarian
/// </summary>
public class TavilyImage
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

// ================================================================
// Kernel Function Result (untuk chatbot)
// ================================================================

/// <summary>
/// Hasil eksekusi kernel function
/// </summary>
public class FunctionResult
{
    public string FunctionName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? Error { get; set; }

    public static FunctionResult Ok(string functionName, string result) =>
        new() { FunctionName = functionName, Success = true, Result = result };

    public static FunctionResult Fail(string functionName, string error) =>
        new() { FunctionName = functionName, Success = false, Error = error };
}

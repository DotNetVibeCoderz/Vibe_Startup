using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PDA.Data;
using PDA.Models;

namespace PDA.Services.RAG;

/// <summary>
/// Service for indexing documents into vector store for RAG.
/// Handles document parsing, chunking, and vector indexing.
/// </summary>
public class RagIndexingService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RagIndexingService> _logger;
    private readonly List<MemoryVectorStore> _vectorStores = new();

    public RagIndexingService(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<RagIndexingService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Scan the KnowledgeBase folder and index new/modified files
    /// </summary>
    public async Task ScanAndIndexAsync(CancellationToken cancellationToken = default)
    {
        var kbPath = _configuration["RAG:KnowledgeBasePath"] ?? "KnowledgeBase";
        if (!Directory.Exists(kbPath))
        {
            Directory.CreateDirectory(kbPath);
            _logger.LogInformation("Created KnowledgeBase folder at: {Path}", kbPath);
        }

        var vectorProvider = _configuration["RAG:VectorProvider"] ?? "InMemory";
        var chunkSize = int.Parse(_configuration["RAG:ChunkSize"] ?? "1000");
        var chunkOverlap = int.Parse(_configuration["RAG:ChunkOverlap"] ?? "200");
        var maxFileSizeMb = int.Parse(_configuration["RAG:MaxFileSizeMb"] ?? "50");
        var maxFileSize = maxFileSizeMb * 1024 * 1024;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get all files recursively
        var files = Directory.GetFiles(kbPath, "*.*", SearchOption.AllDirectories)
            .Where(f => IsSupportedFile(f))
            .ToList();

        foreach (var file in files)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.Length > maxFileSize)
                {
                    _logger.LogWarning("File too large, skipping: {File} ({Size}MB)", file, fileInfo.Length / 1024 / 1024);
                    continue;
                }

                var hash = await ComputeHashAsync(file);
                var relativePath = Path.GetRelativePath(kbPath, file);

                var existing = await db.RagIndexedDocuments
                    .FirstOrDefaultAsync(d => d.FilePath == relativePath);

                if (existing != null && existing.ContentHash == hash)
                {
                    continue;
                }

                var content = await ExtractTextAsync(file);
                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("No text content extracted from: {File}", file);
                    continue;
                }

                var chunks = ChunkText(content, chunkSize, chunkOverlap);
                var store = GetVectorStore(vectorProvider);
                await store.IndexDocumentAsync(file, relativePath, chunks);

                var keywords = ExtractKeywords(content);

                if (existing != null)
                {
                    existing.ChunkCount = chunks.Count;
                    existing.ContentHash = hash;
                    existing.IndexedAt = DateTime.UtcNow;
                    existing.Status = "Indexed";
                    existing.FileSize = fileInfo.Length;
                    existing.FileModifiedAt = fileInfo.LastWriteTimeUtc;
                    existing.Keywords = keywords;
                }
                else
                {
                    db.RagIndexedDocuments.Add(new RagIndexedDocument
                    {
                        FileName = fileInfo.Name,
                        FilePath = relativePath,
                        FileType = fileInfo.Extension.TrimStart('.').ToLower(),
                        FileSize = fileInfo.Length,
                        IndexedAt = DateTime.UtcNow,
                        FileModifiedAt = fileInfo.LastWriteTimeUtc,
                        ChunkCount = chunks.Count,
                        VectorProvider = vectorProvider,
                        ContentHash = hash,
                        Status = "Indexed",
                        Keywords = keywords
                    });
                }

                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Indexed document: {File} ({Chunks} chunks)", file, chunks.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to index file: {File}", file);
            }
        }
    }

    /// <summary>
    /// Search the knowledge base for relevant documents
    /// </summary>
    public async Task<List<(string Content, string Source, double Score)>> SearchAsync(string query, int topK = 5)
    {
        var vectorProvider = _configuration["RAG:VectorProvider"] ?? "InMemory";
        var store = GetVectorStore(vectorProvider);
        return await store.SearchAsync(query, topK);
    }

    private static bool IsSupportedFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLower();
        return ext switch
        {
            ".txt" or ".md" or ".csv" or ".json" or ".xml" or ".html" or ".htm" => true,
            ".pdf" or ".docx" or ".doc" or ".xlsx" or ".xls" or ".pptx" or ".ppt" => true,
            _ => false
        };
    }

    private static async Task<string> ExtractTextAsync(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLower();
        return ext switch
        {
            ".txt" or ".md" or ".csv" or ".json" or ".xml" or ".html" or ".htm" =>
                await File.ReadAllTextAsync(filePath),
            ".pdf" => await ExtractPdfTextAsync(filePath),
            ".docx" => await ExtractDocxTextAsync(filePath),
            ".xlsx" => await ExtractExcelTextAsync(filePath),
            ".pptx" => await ExtractPptxTextAsync(filePath),
            _ => await File.ReadAllTextAsync(filePath)
        };
    }

    private static async Task<string> ExtractPdfTextAsync(string filePath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var readable = Regex.Replace(content, @"[^\x20-\x7E\s]", " ").Replace("  ", " ");
            return readable.Length > 100 ? readable : "[PDF requires dedicated library for full extraction]";
        }
        catch { return "[PDF extraction not available]"; }
    }

    private static async Task<string> ExtractDocxTextAsync(string filePath)
    {
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            return Regex.Replace(content, @"[^\x20-\x7E\s]", " ");
        }
        catch { return "[DOCX extraction not available]"; }
    }

    private static async Task<string> ExtractExcelTextAsync(string filePath)
    {
        try { return await File.ReadAllTextAsync(filePath); }
        catch { return "[Excel extraction not available]"; }
    }

    private static async Task<string> ExtractPptxTextAsync(string filePath)
    {
        try { return await File.ReadAllTextAsync(filePath); }
        catch { return "[PowerPoint extraction not available]"; }
    }

    private static List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        var words = text.Split(' ');
        var currentChunk = new List<string>();
        var currentLength = 0;

        foreach (var word in words)
        {
            if (currentLength + word.Length + 1 > chunkSize && currentChunk.Count > 0)
            {
                chunks.Add(string.Join(" ", currentChunk));
                var overlapWords = currentChunk.Skip(Math.Max(0, currentChunk.Count - overlap)).ToList();
                currentChunk = overlapWords;
                currentLength = overlapWords.Sum(w => w.Length) + overlapWords.Count;
            }
            currentChunk.Add(word);
            currentLength += word.Length + 1;
        }

        if (currentChunk.Count > 0)
            chunks.Add(string.Join(" ", currentChunk));

        return chunks;
    }

    private static string ExtractKeywords(string text)
    {
        var words = Regex.Matches(text.ToLower(), @"\b[a-z]{4,}\b")
            .Select(m => m.Value)
            .Where(w => !StopWords.Contains(w))
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(15)
            .Select(g => g.Key);
        return string.Join(", ", words);
    }

    private static async Task<string> ComputeHashAsync(string filePath)
    {
        using var sha = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha.ComputeHashAsync(stream);
        return Convert.ToHexStringLower(hash);
    }

    private MemoryVectorStore GetVectorStore(string provider)
    {
        lock (_vectorStores)
        {
            var store = _vectorStores.FirstOrDefault(s => s.Provider == provider);
            if (store == null)
            {
                store = new MemoryVectorStore(provider);
                _vectorStores.Add(store);
            }
            return store;
        }
    }

    private static readonly HashSet<string> StopWords = new()
    {
        "the", "and", "that", "have", "for", "not", "with", "this", "from",
        "which", "would", "will", "there", "their", "what", "about", "into",
        "than", "then", "when", "your", "some", "these", "other", "more",
        "dari", "yang", "dan", "ini", "itu", "dengan", "untuk", "pada",
        "adalah", "dalam", "bahwa", "akan", "telah", "atau"
    };
}

/// <summary>
/// Simple in-memory vector store for RAG.
/// </summary>
public class MemoryVectorStore
{
    private readonly List<VectorDocument> _documents = new();
    public string Provider { get; }

    public MemoryVectorStore(string provider) { Provider = provider; }

    public Task IndexDocumentAsync(string filePath, string relativePath, List<string> chunks)
    {
        lock (_documents)
        {
            _documents.RemoveAll(d => d.Source == relativePath);
            foreach (var chunk in chunks)
            {
                _documents.Add(new VectorDocument { Content = chunk, Source = relativePath, IndexedAt = DateTime.UtcNow });
            }
        }
        return Task.CompletedTask;
    }

    public Task<List<(string Content, string Source, double Score)>> SearchAsync(string query, int topK)
    {
        var results = new List<(string Content, string Source, double Score)>();
        lock (_documents)
        {
            var queryTerms = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var doc in _documents)
            {
                var score = CalculateBm25Like(doc.Content.ToLower(), queryTerms);
                if (score > 0) results.Add((doc.Content, doc.Source, score));
            }
        }
        return Task.FromResult(results.OrderByDescending(r => r.Score).Take(topK).ToList());
    }

    private static double CalculateBm25Like(string content, string[] queryTerms)
    {
        double score = 0;
        foreach (var term in queryTerms)
        {
            if (content.Contains(term))
                score += Math.Log(1 + CountOccurrences(content, term));
        }
        return score;
    }

    private static int CountOccurrences(string text, string term)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += term.Length;
        }
        return count;
    }
}

public class VectorDocument
{
    public string Content { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateTime IndexedAt { get; set; }
}

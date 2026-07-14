using System.Text;
using BlazorViz.Data;
using BlazorViz.Services.Ai;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace BlazorViz.Services.Rag;

/// <summary>Extracts plain text from uploaded documents (PDF, Word, Excel, text).</summary>
public static class DocumentTextExtractor
{
    public static readonly string[] SupportedExtensions = [".pdf", ".docx", ".xlsx", ".txt", ".md", ".csv", ".json"];

    public static string Extract(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => FromPdf(filePath),
            ".docx" => FromWord(filePath),
            ".xlsx" => FromExcel(filePath),
            ".txt" or ".md" or ".csv" or ".json" => File.ReadAllText(filePath),
            _ => throw new InvalidOperationException($"Unsupported document type '{ext}'.")
        };
    }

    private static string FromPdf(string path)
    {
        var sb = new StringBuilder();
        using var pdf = PdfDocument.Open(path);
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    private static string FromWord(string path)
    {
        using var doc = WordprocessingDocument.Open(path, false);
        return doc.MainDocumentPart?.Document?.Body?.InnerText ?? "";
    }

    private static string FromExcel(string path)
    {
        var sb = new StringBuilder();
        using var wb = new XLWorkbook(path);
        foreach (var ws in wb.Worksheets)
        {
            sb.AppendLine($"# Sheet: {ws.Name}");
            var range = ws.RangeUsed();
            if (range is null) continue;
            foreach (var row in range.Rows())
                sb.AppendLine(string.Join(" | ", row.Cells().Select(c => c.GetString())));
        }
        return sb.ToString();
    }
}

/// <summary>Document ingestion + semantic search over the configured vector store.</summary>
public sealed class RagService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IEmbedder embedder,
    IVectorIndex index,
    IOptionsMonitor<AiOptions> options,
    ILogger<RagService> log)
{
    public string StoreKind => index.Kind;

    public async Task<RagDocument> IngestAsync(string fileName, string storedPath, string? userName, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var doc = new RagDocument { FileName = fileName, StoragePath = storedPath, UploadedBy = userName };
        db.RagDocuments.Add(doc);
        await db.SaveChangesAsync(ct);

        try
        {
            var text = DocumentTextExtractor.Extract(storedPath);
            var rag = options.CurrentValue.Rag;
            var chunks = Chunk(text, rag.ChunkSize, rag.ChunkOverlap);
            if (chunks.Count == 0) throw new InvalidOperationException("No text could be extracted from the document.");

            var vectors = await embedder.EmbedAsync(chunks, ct);
            var records = chunks.Select((c, i) => new VectorChunk
            {
                Id = $"{doc.Id}:{i}",
                DocumentId = doc.Id,
                FileName = fileName,
                ChunkIndex = i,
                Text = c,
                Vector = vectors[i]
            }).ToList();
            await index.UpsertAsync(records, ct);

            doc.Status = "indexed";
            doc.ChunkCount = chunks.Count;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "RAG ingestion failed for {File}", fileName);
            doc.Status = "failed";
            doc.Error = ex.Message;
        }
        await db.SaveChangesAsync(ct);
        return doc;
    }

    public async Task DeleteAsync(int documentId, CancellationToken ct = default)
    {
        await index.DeleteDocumentAsync(documentId, ct);
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var doc = await db.RagDocuments.FindAsync([documentId], ct);
        if (doc is not null)
        {
            if (!string.IsNullOrWhiteSpace(doc.StoragePath) && File.Exists(doc.StoragePath))
                try { File.Delete(doc.StoragePath); } catch { }
            db.RagDocuments.Remove(doc);
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<List<VectorMatch>> SearchAsync(string query, int? topK = null, CancellationToken ct = default)
    {
        var vectors = await embedder.EmbedAsync([query], ct);
        return await index.SearchAsync(vectors[0], topK ?? options.CurrentValue.Rag.TopK, ct);
    }

    /// <summary>Builds a context block for the chat prompt, or null when the index is empty / nothing matches.</summary>
    public async Task<string?> BuildContextAsync(string query, CancellationToken ct = default)
    {
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            if (!await db.RagDocuments.AnyAsync(d => d.Status == "indexed", ct)) return null;
            var matches = await SearchAsync(query, ct: ct);
            var relevant = matches.Where(m => m.Score > 0.05).ToList();
            if (relevant.Count == 0) return null;
            var sb = new StringBuilder("Relevant excerpts from the user's indexed documents:\n");
            foreach (var m in relevant)
                sb.AppendLine($"--- {m.Chunk.FileName} (chunk {m.Chunk.ChunkIndex}, score {m.Score:0.00}) ---\n{m.Chunk.Text.Trim()}");
            return sb.ToString();
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "RAG context lookup failed");
            return null;
        }
    }

    public static List<string> Chunk(string text, int size, int overlap)
    {
        var cleaned = text.Replace("\r\n", "\n");
        var chunks = new List<string>();
        var pos = 0;
        while (pos < cleaned.Length)
        {
            var len = Math.Min(size, cleaned.Length - pos);
            var slice = cleaned.Substring(pos, len);
            if (pos + len < cleaned.Length)
            {
                var lastBreak = slice.LastIndexOfAny(['\n', '.', '!', '?']);
                if (lastBreak > size / 2) slice = slice[..(lastBreak + 1)];
            }
            var trimmed = slice.Trim();
            if (trimmed.Length > 0) chunks.Add(trimmed);
            pos += Math.Max(1, slice.Length - overlap);
        }
        return chunks;
    }
}

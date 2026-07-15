using AppBender.Core.Data;
using AppBender.Core.Models;
using AppBender.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace AppBender.Core.AI;

public class RagService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ITenantContext tenant,
    IStorageService storage,
    ILlmClient llm) : IRagService
{
    public async Task<List<KnowledgeDocument>> GetDocumentsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.KnowledgeDocuments.AsNoTracking()
            .Where(d => d.TenantId == tenant.TenantId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<KnowledgeDocument> IngestAsync(string fileName, string contentType, Stream content)
    {
        // persist original file
        var storagePath = $"knowledge/{Guid.NewGuid():N}/{fileName}";
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        buffer.Position = 0;
        await storage.SaveAsync(storagePath, buffer, contentType);

        var document = new KnowledgeDocument
        {
            TenantId = tenant.TenantId,
            FileName = fileName,
            ContentType = contentType,
            StoragePath = storagePath,
            SizeBytes = buffer.Length
        };

        try
        {
            buffer.Position = 0;
            var text = DocumentTextExtractor.Extract(fileName, buffer);
            var chunks = DocumentTextExtractor.Chunk(text);

            var chunkEntities = new List<KnowledgeChunk>();
            foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
            {
                var embedding = await llm.EmbedAsync(chunk); // empty when not configured
                chunkEntities.Add(new KnowledgeChunk
                {
                    TenantId = tenant.TenantId,
                    DocumentId = document.Id,
                    FileName = fileName,
                    ChunkIndex = index,
                    Text = chunk,
                    Embedding = embedding
                });
            }

            document.Status = "indexed";
            document.ChunkCount = chunkEntities.Count;

            await using var db = await dbFactory.CreateDbContextAsync();
            db.KnowledgeDocuments.Add(document);
            db.KnowledgeChunks.AddRange(chunkEntities);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            document.Status = "failed";
            document.Error = ex.Message;
            await using var db = await dbFactory.CreateDbContextAsync();
            db.KnowledgeDocuments.Add(document);
            await db.SaveChangesAsync();
        }
        return document;
    }

    public async Task DeleteDocumentAsync(string documentId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var document = await db.KnowledgeDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.TenantId == tenant.TenantId);
        if (document is null) return;
        await db.KnowledgeChunks.Where(c => c.DocumentId == documentId).ExecuteDeleteAsync();
        db.KnowledgeDocuments.Remove(document);
        await db.SaveChangesAsync();
        try { await storage.DeleteAsync(document.StoragePath); } catch { }
    }

    public async Task<List<KnowledgeChunk>> RetrieveAsync(string query, int topK = 5)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var chunks = await db.KnowledgeChunks.AsNoTracking()
            .Where(c => c.TenantId == tenant.TenantId)
            .ToListAsync();
        if (chunks.Count == 0) return [];

        var queryEmbedding = await llm.EmbedAsync(query);
        if (queryEmbedding.Length > 0 && chunks.Any(c => c.EmbeddingJson.Length > 2))
        {
            return chunks
                .Select(c => (Chunk: c, Score: CosineSimilarity(queryEmbedding, c.Embedding)))
                .OrderByDescending(x => x.Score)
                .Take(topK)
                .Select(x => x.Chunk)
                .ToList();
        }

        // keyword fallback: score by term overlap
        var terms = query.ToLowerInvariant()
            .Split([' ', ',', '.', '?', '!', ';', ':'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .Distinct()
            .ToArray();
        return chunks
            .Select(c =>
            {
                var lower = c.Text.ToLowerInvariant();
                var score = terms.Sum(t =>
                {
                    var count = 0;
                    var pos = 0;
                    while ((pos = lower.IndexOf(t, pos, StringComparison.Ordinal)) >= 0) { count++; pos += t.Length; }
                    return count;
                });
                return (Chunk: c, Score: score);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Chunk)
            .ToList();
    }

    public async Task<RagAnswer> AskAsync(string question, CancellationToken ct = default)
    {
        var relevant = await RetrieveAsync(question, 5);
        if (relevant.Count == 0)
            return new RagAnswer("Tidak ada dokumen relevan di knowledge base. Upload dokumen terlebih dahulu di AI Studio → Knowledge Base.", []);

        var context = string.Join("\n\n---\n\n",
            relevant.Select(c => $"[{c.FileName} #chunk{c.ChunkIndex}]\n{c.Text}"));
        var prompt =
            $"Answer the question using ONLY the context below. If the answer is not in the context, say you don't know.\n" +
            $"Cite the source file names you used.\n\nContext:\n{context}\n\nQuestion: {question}";

        var result = await llm.CompleteAsync(
            [new LlmMessage("system", "You answer questions from provided document excerpts, accurately and concisely, in the user's language."),
             new LlmMessage("user", prompt)],
            null, ct);

        var sources = relevant.Select(c => c.FileName).Distinct().ToList();
        return new RagAnswer(result.Text, sources);
    }

    public static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        return magA == 0 || magB == 0 ? 0 : dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}

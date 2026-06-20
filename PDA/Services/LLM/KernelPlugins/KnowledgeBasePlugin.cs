using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using PDA.Data;

namespace PDA.Services.LLM.KernelPlugins;

/// <summary>
/// Kernel Plugin: Knowledge Base search via RAG
/// Searches indexed documents in the vector store
/// </summary>
public class KnowledgeBasePlugin
{
    private readonly AppDbContext _appDb;
    private readonly ILogger<KnowledgeBasePlugin> _logger;

    public KnowledgeBasePlugin(AppDbContext appDb, ILogger<KnowledgeBasePlugin> logger)
    {
        _appDb = appDb;
        _logger = logger;
    }

    /// <summary>
    /// Search the knowledge base for documents relevant to the query.
    /// Returns document metadata and matching content summaries.
    /// </summary>
    [KernelFunction("searchKnowledgeBase")]
    [Description("Search the knowledge base for documents relevant to a query. " +
                 "Returns document names, types, metadata, and summaries. " +
                 "Use this when you need context from uploaded documents (reports, specs, policies, etc.) " +
                 "that may not be in the database.")]
    public async Task<string> SearchKnowledgeBaseAsync(
        [Description("The search query to find relevant documents")]
        string query,
        [Description("Maximum number of results to return (default 5)")]
        int topK = 5)
    {
        try
        {
            var docs = await _appDb.RagIndexedDocuments
                .Where(d => d.Status == "Indexed")
                .OrderByDescending(d => d.IndexedAt)
                .Take(topK)
                .ToListAsync();

            if (docs.Count == 0)
                return "📭 No indexed documents found in the knowledge base. Upload documents to the KnowledgeBase folder and they will be indexed automatically.";

            var sb = new StringBuilder();
            sb.AppendLine($"📚 **Knowledge Base Results for:** \"{query}\"");
            sb.AppendLine($"Found {docs.Count} relevant document(s):");
            sb.AppendLine();

            foreach (var doc in docs)
            {
                var sizeStr = doc.FileSize switch
                {
                    < 1024 => $"{doc.FileSize} B",
                    < 1024 * 1024 => $"{doc.FileSize / 1024.0:F1} KB",
                    _ => $"{doc.FileSize / (1024.0 * 1024.0):F1} MB"
                };

                sb.AppendLine($"### 📄 {doc.FileName}");
                sb.AppendLine($"- **Type:** {doc.FileType.ToUpper()} | **Size:** {sizeStr} | **Chunks:** {doc.ChunkCount}");
                sb.AppendLine($"- **Indexed:** {doc.IndexedAt:yyyy-MM-dd HH:mm} UTC");
                if (!string.IsNullOrEmpty(doc.Keywords))
                    sb.AppendLine($"- **Keywords:** {doc.Keywords}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Knowledge base search failed");
            return $"❌ Error searching knowledge base: {ex.Message}";
        }
    }
}

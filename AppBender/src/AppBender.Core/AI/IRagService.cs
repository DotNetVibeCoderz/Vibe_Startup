using AppBender.Core.Models;

namespace AppBender.Core.AI;

public record RagAnswer(string Answer, List<string> Sources);

/// <summary>Knowledge base: document ingestion, chunk retrieval, and question answering.</summary>
public interface IRagService
{
    Task<List<KnowledgeDocument>> GetDocumentsAsync();
    Task<KnowledgeDocument> IngestAsync(string fileName, string contentType, Stream content);
    Task DeleteDocumentAsync(string documentId);
    /// <summary>Returns the most relevant chunks (vector search when embeddings exist, keyword fallback otherwise).</summary>
    Task<List<KnowledgeChunk>> RetrieveAsync(string query, int topK = 5);
    Task<RagAnswer> AskAsync(string question, CancellationToken ct = default);
}

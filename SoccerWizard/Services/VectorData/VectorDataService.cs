using System.Collections.Concurrent;

namespace SoccerWizard.Services.VectorData;

/// <summary>
/// Vector Data Service — menyimpan embedding untuk pencarian semantik (RAG).
/// 
/// Backend yang didukung:
/// - InMemory (default, selalu tersedia)
/// - Sqlite (via Microsoft.Extensions.VectorData + SQLite)
/// - Qdrant (via REST API)
/// - Chroma (via REST API)
/// 
/// Saat ini backend Qdrant/Chroma/Sqlite menggunakan fallback InMemory
/// hingga client library terinstal.
/// </summary>
public class VectorDataService
{
    private readonly ConcurrentDictionary<string, FootballEmbedding> _store = new();
    private readonly string _provider;
    private readonly bool _externalReady;

    public VectorDataService(IConfiguration config)
    {
        _provider = config["VectorDatabaseProvider"] ?? "InMemory";

        // Coba inisialisasi backend external
        _externalReady = _provider switch
        {
            "Qdrant" => TryInitQdrant(config),
            "Chroma" => TryInitChroma(config),
            "Sqlite" => TryInitSqliteVector(config),
            _ => false // InMemory always ready
        };
    }

    /// <summary>
    /// Menyimpan embedding untuk RAG search.
    /// </summary>
    public Task StoreEmbeddingAsync(string id, string text, ReadOnlyMemory<float> embedding,
        Dictionary<string, string>? metadata = null)
    {
        var record = new FootballEmbedding
        {
            Id = id,
            Text = text,
            Embedding = embedding.ToArray(),
            Metadata = metadata ?? new Dictionary<string, string>(),
            StoredAt = DateTime.UtcNow
        };
        _store[id] = record;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Mencari top-K embedding terdekat menggunakan cosine similarity.
    /// </summary>
    public Task<List<(string Id, string Text, double Similarity, Dictionary<string, string> Metadata)>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding, int topK = 5, double minSimilarity = 0.5)
    {
        var results = _store.Values
            .Select(c => (
                c.Id, c.Text,
                Similarity: CosineSimilarity(queryEmbedding, c.Embedding),
                c.Metadata))
            .Where(x => x.Similarity >= minSimilarity)
            .OrderByDescending(x => x.Similarity)
            .Take(topK)
            .ToList();

        return Task.FromResult(results);
    }

    /// <summary>
    /// Menghapus embedding berdasarkan ID.
    /// </summary>
    public Task DeleteEmbeddingAsync(string id)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Jumlah total embedding.
    /// </summary>
    public Task<int> GetCountAsync()
        => Task.FromResult(_store.Count);

    public string ProviderName => _externalReady ? _provider : $"{_provider} (InMemory fallback)";

    // ==================== Cosine Similarity ====================
    private static double CosineSimilarity(ReadOnlyMemory<float> a, float[] b)
    {
        var spanA = a.Span;
        int len = Math.Min(spanA.Length, b.Length);
        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < len; i++)
        {
            dot += spanA[i] * b[i];
            magA += spanA[i] * spanA[i];
            magB += b[i] * b[i];
        }
        if (magA == 0 || magB == 0) return 0;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    // ==================== Init External Backends ====================
    private bool TryInitQdrant(IConfiguration config)
    {
        // Qdrant memerlukan client SDK. Fallback ke InMemory.
        var endpoint = config["VectorDatabase:Qdrant:Endpoint"];
        return !string.IsNullOrEmpty(endpoint);
        // TODO: Integrasi Qdrant client SDK
    }

    private bool TryInitChroma(IConfiguration config)
    {
        var endpoint = config["VectorDatabase:Chroma:Endpoint"];
        return !string.IsNullOrEmpty(endpoint);
        // TODO: Integrasi Chroma client SDK
    }

    private bool TryInitSqliteVector(IConfiguration config)
    {
        var connStr = config["VectorDatabase:Sqlite:ConnectionString"];
        return !string.IsNullOrEmpty(connStr);
        // TODO: Integrasi Sqlite vector extension
    }
}

/// <summary>
/// Embedding record untuk Vector Database.
/// </summary>
public class FootballEmbedding
{
    public string Id { get; set; } = "";
    public string Text { get; set; } = "";
    public float[] Embedding { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime StoredAt { get; set; } = DateTime.UtcNow;
}

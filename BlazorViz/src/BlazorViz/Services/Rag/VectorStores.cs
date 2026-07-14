using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using BlazorViz.Services.Ai;

namespace BlazorViz.Services.Rag;

/// <summary>One indexed chunk. Annotated with Microsoft.Extensions.VectorData attributes.</summary>
public sealed class VectorChunk
{
    [VectorStoreKey] public string Id { get; set; } = "";
    [VectorStoreData] public int DocumentId { get; set; }
    [VectorStoreData] public string FileName { get; set; } = "";
    [VectorStoreData] public int ChunkIndex { get; set; }
    [VectorStoreData] public string Text { get; set; } = "";
    [VectorStoreVector(384)] public float[] Vector { get; set; } = [];
}

public sealed record VectorMatch(VectorChunk Chunk, double Score);

// ---------------------------------------------------------------------------
// Embedders
// ---------------------------------------------------------------------------

public interface IEmbedder
{
    Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
    int Dimensions { get; }
}

/// <summary>
/// Offline deterministic embedder (hashed bag-of-words + character trigrams, L2-normalized).
/// No API key needed — good enough for demo/keyword-ish retrieval; swap to OpenAI/Ollama for production.
/// </summary>
public sealed class LocalHashEmbedder(int dimensions = 384) : IEmbedder
{
    public int Dimensions => dimensions;

    public Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default) =>
        Task.FromResult(texts.Select(Embed).ToArray());

    private float[] Embed(string text)
    {
        var vec = new float[dimensions];
        var tokens = text.ToLowerInvariant()
            .Split([' ', '\n', '\t', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '"', '\''], StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            Add(vec, token, 1.0f);
            for (var i = 0; i + 3 <= token.Length; i++)
                Add(vec, token.Substring(i, 3), 0.35f);
        }
        var norm = MathF.Sqrt(vec.Sum(v => v * v));
        if (norm > 0) for (var i = 0; i < vec.Length; i++) vec[i] /= norm;
        return vec;
    }

    private void Add(float[] vec, string token, float weight)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(token));
        var idx = Math.Abs(BitConverter.ToInt32(hash, 0)) % dimensions;
        var sign = (hash[4] & 1) == 0 ? 1 : -1;
        vec[idx] += sign * weight;
    }
}

/// <summary>Calls an OpenAI-compatible /embeddings endpoint (OpenAI, Azure gateway, LM Studio…).</summary>
public sealed class OpenAIEmbedder(IHttpClientFactory httpFactory, EmbeddingOptions opts) : IEmbedder
{
    public int Dimensions => opts.Dimensions;

    public async Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var client = httpFactory.CreateClient("connector");
        var endpoint = (opts.Endpoint?.TrimEnd('/') ?? "https://api.openai.com/v1") + "/embeddings";
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { model = opts.Model, input = texts }), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new("Bearer", opts.ApiKey);
        using var res = await client.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(d => d.GetProperty("embedding").EnumerateArray().Select(e => e.GetSingle()).ToArray())
            .ToArray();
    }
}

/// <summary>Calls Ollama's /api/embed endpoint.</summary>
public sealed class OllamaEmbedder(IHttpClientFactory httpFactory, EmbeddingOptions opts) : IEmbedder
{
    public int Dimensions => opts.Dimensions;

    public async Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var client = httpFactory.CreateClient("connector");
        var endpoint = (opts.Endpoint?.TrimEnd('/') ?? "http://localhost:11434") + "/api/embed";
        using var res = await client.PostAsync(endpoint,
            new StringContent(JsonSerializer.Serialize(new { model = opts.Model, input = texts }), Encoding.UTF8, "application/json"), ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("embeddings").EnumerateArray()
            .Select(d => d.EnumerateArray().Select(e => e.GetSingle()).ToArray())
            .ToArray();
    }
}

// ---------------------------------------------------------------------------
// Vector indexes
// ---------------------------------------------------------------------------

public interface IVectorIndex
{
    string Kind { get; }
    Task UpsertAsync(IReadOnlyList<VectorChunk> chunks, CancellationToken ct = default);
    Task<List<VectorMatch>> SearchAsync(float[] query, int topK, CancellationToken ct = default);
    Task DeleteDocumentAsync(int documentId, CancellationToken ct = default);
}

/// <summary>Default store: in-process cosine search, persisted to App_Data/vector-index.json.</summary>
public sealed class InMemoryVectorIndex : IVectorIndex
{
    private readonly Dictionary<string, VectorChunk> _chunks = new();
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string Kind => "InMemory";

    public InMemoryVectorIndex(IWebHostEnvironment env)
    {
        _path = Path.Combine(env.ContentRootPath, "App_Data", "vector-index.json");
        if (File.Exists(_path))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<List<VectorChunk>>(File.ReadAllText(_path)) ?? [];
                foreach (var c in loaded) _chunks[c.Id] = c;
            }
            catch { /* corrupted index → start empty */ }
        }
    }

    public async Task UpsertAsync(IReadOnlyList<VectorChunk> chunks, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            foreach (var c in chunks) _chunks[c.Id] = c;
            await PersistAsync(ct);
        }
        finally { _lock.Release(); }
    }

    public Task<List<VectorMatch>> SearchAsync(float[] query, int topK, CancellationToken ct = default)
    {
        var results = _chunks.Values
            .Select(c => new VectorMatch(c, Cosine(query, c.Vector)))
            .OrderByDescending(m => m.Score)
            .Take(topK)
            .ToList();
        return Task.FromResult(results);
    }

    public async Task DeleteDocumentAsync(int documentId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            foreach (var key in _chunks.Where(kv => kv.Value.DocumentId == documentId).Select(kv => kv.Key).ToList())
                _chunks.Remove(key);
            await PersistAsync(ct);
        }
        finally { _lock.Release(); }
    }

    private async Task PersistAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(_chunks.Values.ToList()), ct);
    }

    public static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        return na == 0 || nb == 0 ? 0 : dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }
}

/// <summary>Qdrant via REST API.</summary>
public sealed class QdrantVectorIndex(IHttpClientFactory httpFactory, IOptionsMonitor<AiOptions> options) : IVectorIndex
{
    public string Kind => "Qdrant";
    private RagOptions Rag => options.CurrentValue.Rag;

    private HttpClient Client()
    {
        var c = httpFactory.CreateClient("connector");
        c.BaseAddress = new Uri(Rag.QdrantEndpoint ?? "http://localhost:6333");
        if (!string.IsNullOrWhiteSpace(Rag.QdrantApiKey)) c.DefaultRequestHeaders.Add("api-key", Rag.QdrantApiKey);
        return c;
    }

    private async Task EnsureCollectionAsync(HttpClient client, int dims, CancellationToken ct)
    {
        var res = await client.GetAsync($"/collections/{Rag.Collection}", ct);
        if (res.IsSuccessStatusCode) return;
        await client.PutAsync($"/collections/{Rag.Collection}",
            JsonContent(JsonSerializer.Serialize(new { vectors = new { size = dims, distance = "Cosine" } })), ct);
    }

    public async Task UpsertAsync(IReadOnlyList<VectorChunk> chunks, CancellationToken ct = default)
    {
        if (chunks.Count == 0) return;
        var client = Client();
        await EnsureCollectionAsync(client, chunks[0].Vector.Length, ct);
        var points = chunks.Select(c => new
        {
            id = StableGuid(c.Id),
            vector = c.Vector,
            payload = new { docId = c.DocumentId, fileName = c.FileName, chunkIndex = c.ChunkIndex, text = c.Text }
        });
        var res = await client.PutAsync($"/collections/{Rag.Collection}/points?wait=true",
            JsonContent(JsonSerializer.Serialize(new { points })), ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task<List<VectorMatch>> SearchAsync(float[] query, int topK, CancellationToken ct = default)
    {
        var client = Client();
        var res = await client.PostAsync($"/collections/{Rag.Collection}/points/search",
            JsonContent(JsonSerializer.Serialize(new { vector = query, limit = topK, with_payload = true })), ct);
        if (!res.IsSuccessStatusCode) return [];
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var matches = new List<VectorMatch>();
        foreach (var hit in doc.RootElement.GetProperty("result").EnumerateArray())
        {
            var payload = hit.GetProperty("payload");
            matches.Add(new VectorMatch(new VectorChunk
            {
                Id = hit.GetProperty("id").ToString(),
                DocumentId = payload.GetProperty("docId").GetInt32(),
                FileName = payload.GetProperty("fileName").GetString() ?? "",
                ChunkIndex = payload.GetProperty("chunkIndex").GetInt32(),
                Text = payload.GetProperty("text").GetString() ?? ""
            }, hit.GetProperty("score").GetDouble()));
        }
        return matches;
    }

    public async Task DeleteDocumentAsync(int documentId, CancellationToken ct = default)
    {
        var client = Client();
        await client.PostAsync($"/collections/{Rag.Collection}/points/delete?wait=true",
            JsonContent(JsonSerializer.Serialize(new
            {
                filter = new { must = new[] { new { key = "docId", match = new { value = documentId } } } }
            })), ct);
    }

    private static StringContent JsonContent(string json) => new(json, Encoding.UTF8, "application/json");

    private static string StableGuid(string id)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(id));
        return new Guid(hash).ToString();
    }
}

/// <summary>Chroma via REST API (v1).</summary>
public sealed class ChromaVectorIndex(IHttpClientFactory httpFactory, IOptionsMonitor<AiOptions> options) : IVectorIndex
{
    public string Kind => "Chroma";
    private RagOptions Rag => options.CurrentValue.Rag;
    private string? _collectionId;

    private HttpClient Client()
    {
        var c = httpFactory.CreateClient("connector");
        c.BaseAddress = new Uri(Rag.ChromaEndpoint ?? "http://localhost:8000");
        return c;
    }

    private async Task<string> CollectionIdAsync(HttpClient client, CancellationToken ct)
    {
        if (_collectionId is not null) return _collectionId;
        var res = await client.PostAsync("/api/v1/collections",
            new StringContent(JsonSerializer.Serialize(new { name = Rag.Collection, get_or_create = true }), Encoding.UTF8, "application/json"), ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return _collectionId = doc.RootElement.GetProperty("id").GetString()!;
    }

    public async Task UpsertAsync(IReadOnlyList<VectorChunk> chunks, CancellationToken ct = default)
    {
        if (chunks.Count == 0) return;
        var client = Client();
        var id = await CollectionIdAsync(client, ct);
        var payload = new
        {
            ids = chunks.Select(c => c.Id).ToArray(),
            embeddings = chunks.Select(c => c.Vector).ToArray(),
            documents = chunks.Select(c => c.Text).ToArray(),
            metadatas = chunks.Select(c => new { docId = c.DocumentId, fileName = c.FileName, chunkIndex = c.ChunkIndex }).ToArray()
        };
        var res = await client.PostAsync($"/api/v1/collections/{id}/upsert",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task<List<VectorMatch>> SearchAsync(float[] query, int topK, CancellationToken ct = default)
    {
        var client = Client();
        var id = await CollectionIdAsync(client, ct);
        var res = await client.PostAsync($"/api/v1/collections/{id}/query",
            new StringContent(JsonSerializer.Serialize(new
            {
                query_embeddings = new[] { query },
                n_results = topK,
                include = new[] { "documents", "metadatas", "distances" }
            }), Encoding.UTF8, "application/json"), ct);
        if (!res.IsSuccessStatusCode) return [];
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var ids = root.GetProperty("ids")[0];
        var docs = root.GetProperty("documents")[0];
        var metas = root.GetProperty("metadatas")[0];
        var dists = root.GetProperty("distances")[0];
        var matches = new List<VectorMatch>();
        for (var i = 0; i < ids.GetArrayLength(); i++)
        {
            var meta = metas[i];
            matches.Add(new VectorMatch(new VectorChunk
            {
                Id = ids[i].GetString() ?? "",
                DocumentId = meta.GetProperty("docId").GetInt32(),
                FileName = meta.GetProperty("fileName").GetString() ?? "",
                ChunkIndex = meta.GetProperty("chunkIndex").GetInt32(),
                Text = docs[i].GetString() ?? ""
            }, 1 - dists[i].GetDouble()));
        }
        return matches;
    }

    public async Task DeleteDocumentAsync(int documentId, CancellationToken ct = default)
    {
        var client = Client();
        var id = await CollectionIdAsync(client, ct);
        await client.PostAsync($"/api/v1/collections/{id}/delete",
            new StringContent(JsonSerializer.Serialize(new { where = new { docId = documentId } }), Encoding.UTF8, "application/json"), ct);
    }
}

/// <summary>Azure AI Search via REST API.</summary>
public sealed class AzureAISearchVectorIndex(IHttpClientFactory httpFactory, IOptionsMonitor<AiOptions> options) : IVectorIndex
{
    public string Kind => "AzureAISearch";
    private RagOptions Rag => options.CurrentValue.Rag;
    private const string ApiVersion = "2024-07-01";
    private bool _indexEnsured;

    private HttpClient Client()
    {
        var c = httpFactory.CreateClient("connector");
        c.BaseAddress = new Uri(Rag.AzureSearchEndpoint ?? throw new InvalidOperationException("Ai:Rag:AzureSearchEndpoint not configured."));
        c.DefaultRequestHeaders.Add("api-key", Rag.AzureSearchApiKey ?? "");
        return c;
    }

    private async Task EnsureIndexAsync(HttpClient client, int dims, CancellationToken ct)
    {
        if (_indexEnsured) return;
        var index = new
        {
            name = Rag.Collection,
            fields = new object[]
            {
                new { name = "id", type = "Edm.String", key = true, filterable = true },
                new { name = "docId", type = "Edm.Int32", filterable = true },
                new { name = "fileName", type = "Edm.String", filterable = true },
                new { name = "chunkIndex", type = "Edm.Int32", filterable = false },
                new { name = "text", type = "Edm.String", searchable = true },
                new { name = "vector", type = "Collection(Edm.Single)", searchable = true, dimensions = dims, vectorSearchProfile = "default" }
            },
            vectorSearch = new
            {
                algorithms = new object[] { new { name = "hnsw", kind = "hnsw" } },
                profiles = new object[] { new { name = "default", algorithm = "hnsw" } }
            }
        };
        var res = await client.PutAsync($"/indexes/{Rag.Collection}?api-version={ApiVersion}",
            new StringContent(JsonSerializer.Serialize(index), Encoding.UTF8, "application/json"), ct);
        res.EnsureSuccessStatusCode();
        _indexEnsured = true;
    }

    public async Task UpsertAsync(IReadOnlyList<VectorChunk> chunks, CancellationToken ct = default)
    {
        if (chunks.Count == 0) return;
        var client = Client();
        await EnsureIndexAsync(client, chunks[0].Vector.Length, ct);
        var payload = new
        {
            value = chunks.Select(c => new
            {
                id = c.Id.Replace(":", "_"),
                docId = c.DocumentId,
                fileName = c.FileName,
                chunkIndex = c.ChunkIndex,
                text = c.Text,
                vector = c.Vector,
                searchAction = "mergeOrUpload"
            })
        };
        var json = JsonSerializer.Serialize(payload).Replace("\"searchAction\"", "\"@search.action\"");
        var res = await client.PostAsync($"/indexes/{Rag.Collection}/docs/index?api-version={ApiVersion}",
            new StringContent(json, Encoding.UTF8, "application/json"), ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task<List<VectorMatch>> SearchAsync(float[] query, int topK, CancellationToken ct = default)
    {
        var client = Client();
        var body = new
        {
            count = false,
            select = "id,docId,fileName,chunkIndex,text",
            vectorQueries = new[] { new { kind = "vector", vector = query, fields = "vector", k = topK } }
        };
        var res = await client.PostAsync($"/indexes/{Rag.Collection}/docs/search?api-version={ApiVersion}",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);
        if (!res.IsSuccessStatusCode) return [];
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var matches = new List<VectorMatch>();
        foreach (var hit in doc.RootElement.GetProperty("value").EnumerateArray())
            matches.Add(new VectorMatch(new VectorChunk
            {
                Id = hit.GetProperty("id").GetString() ?? "",
                DocumentId = hit.GetProperty("docId").GetInt32(),
                FileName = hit.GetProperty("fileName").GetString() ?? "",
                ChunkIndex = hit.GetProperty("chunkIndex").GetInt32(),
                Text = hit.GetProperty("text").GetString() ?? ""
            }, hit.GetProperty("@search.score").GetDouble()));
        return matches;
    }

    public async Task DeleteDocumentAsync(int documentId, CancellationToken ct = default)
    {
        var client = Client();
        // find ids for the document then delete them
        var res = await client.PostAsync($"/indexes/{Rag.Collection}/docs/search?api-version={ApiVersion}",
            new StringContent(JsonSerializer.Serialize(new { filter = $"docId eq {documentId}", select = "id", top = 1000 }), Encoding.UTF8, "application/json"), ct);
        if (!res.IsSuccessStatusCode) return;
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        var ids = doc.RootElement.GetProperty("value").EnumerateArray()
            .Select(v => v.GetProperty("id").GetString()).Where(s => s is not null).ToList();
        if (ids.Count == 0) return;
        var deleteJson = JsonSerializer.Serialize(new { value = ids.Select(id => new { id, searchAction = "delete" }) })
            .Replace("\"searchAction\"", "\"@search.action\"");
        await client.PostAsync($"/indexes/{Rag.Collection}/docs/index?api-version={ApiVersion}",
            new StringContent(deleteJson, Encoding.UTF8, "application/json"), ct);
    }
}

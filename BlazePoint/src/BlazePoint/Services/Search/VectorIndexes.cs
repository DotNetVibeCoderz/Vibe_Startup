using System.Text.Json;

namespace BlazePoint.Services.Search;

/// <summary>External vector index (Qdrant / Chroma). Local mode is handled in-database by SearchService.</summary>
public interface IVectorIndex
{
    Task UpsertAsync(int id, float[] vector, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task<List<(int Id, double Score)>> SearchAsync(float[] vector, int topK, CancellationToken ct = default);
}

public sealed class QdrantVectorIndex(HttpClient http, string endpoint, string collection) : IVectorIndex
{
    private bool _ready;

    private async Task EnsureCollectionAsync(int dims, CancellationToken ct)
    {
        if (_ready) return;
        var url = $"{endpoint.TrimEnd('/')}/collections/{collection}";
        var check = await http.GetAsync(url, ct);
        if (!check.IsSuccessStatusCode)
        {
            var body = new { vectors = new { size = dims, distance = "Cosine" } };
            (await http.PutAsJsonAsync(url, body, ct)).EnsureSuccessStatusCode();
        }
        _ready = true;
    }

    public async Task UpsertAsync(int id, float[] vector, CancellationToken ct = default)
    {
        await EnsureCollectionAsync(vector.Length, ct);
        var body = new { points = new[] { new { id, vector } } };
        (await http.PutAsJsonAsync($"{endpoint.TrimEnd('/')}/collections/{collection}/points?wait=true", body, ct))
            .EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var body = new { points = new[] { id } };
        await http.PostAsJsonAsync($"{endpoint.TrimEnd('/')}/collections/{collection}/points/delete?wait=true", body, ct);
    }

    public async Task<List<(int, double)>> SearchAsync(float[] vector, int topK, CancellationToken ct = default)
    {
        await EnsureCollectionAsync(vector.Length, ct);
        var res = await http.PostAsJsonAsync($"{endpoint.TrimEnd('/')}/collections/{collection}/points/search",
            new { vector, limit = topK }, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(ct);
        return json.GetProperty("result").EnumerateArray()
            .Select(r => (r.GetProperty("id").GetInt32(), r.GetProperty("score").GetDouble()))
            .ToList();
    }
}

public sealed class ChromaVectorIndex(HttpClient http, string endpoint, string collection) : IVectorIndex
{
    private string? _collectionId;

    private async Task<string> EnsureCollectionAsync(CancellationToken ct)
    {
        if (_collectionId is not null) return _collectionId;
        var res = await http.PostAsJsonAsync($"{endpoint.TrimEnd('/')}/api/v1/collections",
            new { name = collection, get_or_create = true }, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(ct);
        _collectionId = json.GetProperty("id").GetString()!;
        return _collectionId;
    }

    public async Task UpsertAsync(int id, float[] vector, CancellationToken ct = default)
    {
        var cid = await EnsureCollectionAsync(ct);
        var body = new { ids = new[] { id.ToString() }, embeddings = new[] { vector } };
        (await http.PostAsJsonAsync($"{endpoint.TrimEnd('/')}/api/v1/collections/{cid}/upsert", body, ct))
            .EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var cid = await EnsureCollectionAsync(ct);
        await http.PostAsJsonAsync($"{endpoint.TrimEnd('/')}/api/v1/collections/{cid}/delete",
            new { ids = new[] { id.ToString() } }, ct);
    }

    public async Task<List<(int, double)>> SearchAsync(float[] vector, int topK, CancellationToken ct = default)
    {
        var cid = await EnsureCollectionAsync(ct);
        var res = await http.PostAsJsonAsync($"{endpoint.TrimEnd('/')}/api/v1/collections/{cid}/query",
            new { query_embeddings = new[] { vector }, n_results = topK }, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(ct);
        var ids = json.GetProperty("ids")[0].EnumerateArray().Select(x => int.Parse(x.GetString()!)).ToList();
        var distances = json.GetProperty("distances")[0].EnumerateArray().Select(x => x.GetDouble()).ToList();
        // Chroma returns distance (lower = closer); convert to similarity-like score
        return ids.Select((id, i) => (id, 1.0 / (1.0 + distances[i]))).ToList();
    }
}

using Microsoft.Extensions.AI;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BlazePoint.Services.Search;

/// <summary>
/// Deterministic local embedding (hashed bag-of-words, 384 dims). Zero-dependency fallback
/// so semantic search works out-of-the-box without any API key.
/// </summary>
public sealed class LocalHashEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public const int Dimensions = 384;

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = new GeneratedEmbeddings<Embedding<float>>(
            values.Select(v => new Embedding<float>(Embed(v))));
        return Task.FromResult(result);
    }

    public static float[] Embed(string text)
    {
        var vector = new float[Dimensions];
        var tokens = text.ToLowerInvariant()
            .Split([' ', '\n', '\t', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '"', '\''],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(token));
            var idx = Math.Abs(BitConverter.ToInt32(hash, 0)) % Dimensions;
            var sign = hash[4] % 2 == 0 ? 1f : -1f;
            vector[idx] += sign;
            // bigram-ish smoothing: second slot per token
            var idx2 = Math.Abs(BitConverter.ToInt32(hash, 8)) % Dimensions;
            vector[idx2] += sign * 0.5f;
        }
        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        if (norm > 0) for (var i = 0; i < vector.Length; i++) vector[i] /= norm;
        return vector;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}

/// <summary>OpenAI embeddings via REST (text-embedding-3-small by default).</summary>
public sealed class OpenAIEmbeddingGenerator(HttpClient http, string apiKey, string model)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
        req.Headers.Authorization = new("Bearer", apiKey);
        req.Content = JsonContent.Create(new { input = values.ToArray(), model });
        using var res = await http.SendAsync(req, cancellationToken);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var embeddings = json.GetProperty("data").EnumerateArray()
            .Select(d => new Embedding<float>(
                d.GetProperty("embedding").EnumerateArray().Select(x => (float)x.GetDouble()).ToArray()));
        return new GeneratedEmbeddings<Embedding<float>>(embeddings);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}

/// <summary>Ollama embeddings via REST (nomic-embed-text by default).</summary>
public sealed class OllamaEmbeddingGenerator(HttpClient http, string endpoint, string model)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values, EmbeddingGenerationOptions? options = null, CancellationToken cancellationToken = default)
    {
        var list = new List<Embedding<float>>();
        foreach (var value in values)
        {
            using var res = await http.PostAsJsonAsync($"{endpoint.TrimEnd('/')}/api/embeddings",
                new { model, prompt = value }, cancellationToken);
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            list.Add(new Embedding<float>(
                json.GetProperty("embedding").EnumerateArray().Select(x => (float)x.GetDouble()).ToArray()));
        }
        return new GeneratedEmbeddings<Embedding<float>>(list);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}

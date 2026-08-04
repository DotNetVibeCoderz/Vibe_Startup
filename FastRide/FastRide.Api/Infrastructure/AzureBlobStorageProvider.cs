using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using FastRide.Shared.Storage;

namespace FastRide.Api.Infrastructure;

/// <summary>
/// Azure Blob Storage over the REST API, authenticated with a Shared Key signature.
///
/// The previous version sent no Authorization header at all, so every request came back
/// 403 unless the container happened to be public.
/// </summary>
public sealed class AzureBlobStorageProvider : IStorageProvider
{
    private const string ApiVersion = "2021-08-06";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AzureBlobStorageProvider> _logger;
    private readonly string _containerName, _accountName, _accountKey, _blobEndpoint;

    public string Name => "AzureBlob";

    public AzureBlobStorageProvider(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<AzureBlobStorageProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _containerName = config["Storage:Azure:Container"] ?? "fastride-photos";

        var parts = (config["Storage:Azure:ConnectionString"] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .ToDictionary(pair => pair[0].Trim(), pair => pair[1].Trim(), StringComparer.OrdinalIgnoreCase);

        _accountName = parts.GetValueOrDefault("AccountName", string.Empty);
        _accountKey = parts.GetValueOrDefault("AccountKey", string.Empty);

        var protocol = parts.GetValueOrDefault("DefaultEndpointsProtocol", "https");
        var suffix = parts.GetValueOrDefault("EndpointSuffix", "core.windows.net");
        _blobEndpoint = parts.TryGetValue("BlobEndpoint", out var explicitEndpoint)
            ? explicitEndpoint.TrimEnd('/')
            : $"{protocol}://{_accountName}.blob.{suffix}";
    }

    public async Task<string> UploadAsync(string fileName, byte[] data, string contentType, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, BlobUrl(fileName))
        {
            Content = new ByteArrayContent(data)
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        request.Content.Headers.ContentLength = data.LongLength;
        request.Headers.TryAddWithoutValidation("x-ms-blob-type", "BlockBlob");

        Sign(request, data.LongLength, contentType);

        using var client = _httpClientFactory.CreateClient(nameof(AzureBlobStorageProvider));
        using var response = await client.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Azure Blob upload of {File} failed with {Status}: {Body}",
                fileName, (int)response.StatusCode, body);
            throw new HttpRequestException($"Azure Blob upload failed with {(int)response.StatusCode}.");
        }

        return BlobUrl(fileName);
    }

    public async Task<byte[]?> DownloadAsync(string fileName, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BlobUrl(fileName));
        Sign(request, 0, null);

        using var client = _httpClientFactory.CreateClient(nameof(AzureBlobStorageProvider));
        using var response = await client.SendAsync(request, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(ct) : null;
    }

    public async Task<bool> DeleteAsync(string fileName, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, BlobUrl(fileName));
        Sign(request, 0, null);

        using var client = _httpClientFactory.CreateClient(nameof(AzureBlobStorageProvider));
        using var response = await client.SendAsync(request, ct);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ExistsAsync(string fileName, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, BlobUrl(fileName));
        Sign(request, 0, null);

        using var client = _httpClientFactory.CreateClient(nameof(AzureBlobStorageProvider));
        using var response = await client.SendAsync(request, ct);

        return response.IsSuccessStatusCode;
    }

    public string? ResolveFileName(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        var prefix = $"{_blobEndpoint}/{_containerName}/";
        return url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? url[prefix.Length..] : null;
    }

    private string BlobUrl(string fileName) =>
        $"{_blobEndpoint}/{_containerName}/{string.Join('/', fileName.Split('/').Select(Uri.EscapeDataString))}";

    // ─────────────────────── Shared Key signature ───────────────────────

    private void Sign(HttpRequestMessage request, long contentLength, string? contentType)
    {
        var date = DateTime.UtcNow.ToString("R", CultureInfo.InvariantCulture);
        request.Headers.TryAddWithoutValidation("x-ms-date", date);
        request.Headers.TryAddWithoutValidation("x-ms-version", ApiVersion);

        if (string.IsNullOrEmpty(_accountKey)) return;

        // Canonicalized x-ms-* headers, lower-cased and ordered by name.
        var msHeaders = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["x-ms-date"] = date,
            ["x-ms-version"] = ApiVersion
        };
        if (request.Headers.TryGetValues("x-ms-blob-type", out var blobType))
            msHeaders["x-ms-blob-type"] = string.Join(",", blobType);

        var canonicalizedHeaders = string.Concat(msHeaders.Select(h => $"{h.Key}:{h.Value}\n"));

        var uri = request.RequestUri!;
        var canonicalizedResource = $"/{_accountName}{uri.AbsolutePath}";
        if (!string.IsNullOrEmpty(uri.Query))
        {
            var queryParts = uri.Query.TrimStart('?')
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Split('=', 2))
                .Select(pair => $"{Uri.UnescapeDataString(pair[0]).ToLowerInvariant()}:{(pair.Length > 1 ? Uri.UnescapeDataString(pair[1]) : string.Empty)}")
                .OrderBy(part => part, StringComparer.Ordinal);

            canonicalizedResource += "\n" + string.Join('\n', queryParts);
        }

        // Field order is fixed by the Shared Key spec; blanks are still required.
        var stringToSign = string.Join('\n',
            request.Method.Method,
            string.Empty,                                   // Content-Encoding
            string.Empty,                                   // Content-Language
            contentLength > 0 ? contentLength.ToString(CultureInfo.InvariantCulture) : string.Empty,
            string.Empty,                                   // Content-MD5
            contentType ?? string.Empty,
            string.Empty,                                   // Date (superseded by x-ms-date)
            string.Empty,                                   // If-Modified-Since
            string.Empty,                                   // If-Match
            string.Empty,                                   // If-None-Match
            string.Empty,                                   // If-Unmodified-Since
            string.Empty,                                   // Range
            canonicalizedHeaders + canonicalizedResource);

        var signature = Convert.ToBase64String(
            HMACSHA256.HashData(Convert.FromBase64String(_accountKey), Encoding.UTF8.GetBytes(stringToSign)));

        request.Headers.TryAddWithoutValidation("Authorization", $"SharedKey {_accountName}:{signature}");
    }
}

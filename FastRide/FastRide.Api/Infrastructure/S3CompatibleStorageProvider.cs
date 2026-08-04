using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using FastRide.Shared.Storage;

namespace FastRide.Api.Infrastructure;

/// <summary>
/// Generic S3-compatible storage (AWS S3, MinIO, DigitalOcean Spaces, ...).
///
/// Requests are signed with AWS Signature Version 4 using path-style addressing, which is
/// what MinIO and most S3 clones expect. The previous implementation emitted a placeholder
/// Authorization header, so every call was rejected.
/// </summary>
public sealed class S3CompatibleStorageProvider : IStorageProvider
{
    private const string Algorithm = "AWS4-HMAC-SHA256";
    private const string Service = "s3";
    private static readonly string EmptyPayloadHash = ToHex(SHA256.HashData([]));

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<S3CompatibleStorageProvider> _logger;
    private readonly string _endpoint, _bucket, _accessKey, _secretKey, _region, _publicUrl;

    public string Name => "S3";

    public S3CompatibleStorageProvider(
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<S3CompatibleStorageProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _endpoint = (config["Storage:S3:Endpoint"] ?? "https://s3.amazonaws.com").TrimEnd('/');
        _bucket = config["Storage:S3:Bucket"] ?? "fastride";
        _accessKey = config["Storage:S3:AccessKey"] ?? "";
        _secretKey = config["Storage:S3:SecretKey"] ?? "";
        _region = config["Storage:S3:Region"] ?? "us-east-1";
        _publicUrl = (config["Storage:S3:PublicUrl"] ?? $"{_endpoint}/{_bucket}").TrimEnd('/');
    }

    public async Task<string> UploadAsync(string fileName, byte[] data, string contentType, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, ObjectUrl(fileName))
        {
            Content = new ByteArrayContent(data)
        };
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        request.Headers.TryAddWithoutValidation("x-amz-acl", "public-read");

        Sign(request, data);

        using var client = _httpClientFactory.CreateClient(nameof(S3CompatibleStorageProvider));
        using var response = await client.SendAsync(request, ct);
        await EnsureSuccess(response, "upload", fileName, ct);

        return $"{_publicUrl}/{fileName}";
    }

    public async Task<byte[]?> DownloadAsync(string fileName, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ObjectUrl(fileName));
        Sign(request, null);

        using var client = _httpClientFactory.CreateClient(nameof(S3CompatibleStorageProvider));
        using var response = await client.SendAsync(request, ct);

        return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(ct) : null;
    }

    public async Task<bool> DeleteAsync(string fileName, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, ObjectUrl(fileName));
        Sign(request, null);

        using var client = _httpClientFactory.CreateClient(nameof(S3CompatibleStorageProvider));
        using var response = await client.SendAsync(request, ct);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> ExistsAsync(string fileName, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, ObjectUrl(fileName));
        Sign(request, null);

        using var client = _httpClientFactory.CreateClient(nameof(S3CompatibleStorageProvider));
        using var response = await client.SendAsync(request, ct);

        return response.IsSuccessStatusCode;
    }

    public string? ResolveFileName(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (var prefix in new[] { _publicUrl, $"{_endpoint}/{_bucket}" })
        {
            if (url.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
                return url[(prefix.Length + 1)..];
        }

        return null;
    }

    private string ObjectUrl(string fileName) => $"{_endpoint}/{_bucket}/{EncodePath(fileName)}";

    private async Task EnsureSuccess(HttpResponseMessage response, string operation, string fileName, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct);
        _logger.LogError("S3 {Operation} of {File} failed with {Status}: {Body}",
            operation, fileName, (int)response.StatusCode, body);

        throw new HttpRequestException($"S3 {operation} failed with {(int)response.StatusCode}.");
    }

    // ───────────────────────── AWS Signature V4 ─────────────────────────

    private void Sign(HttpRequestMessage request, byte[]? payload)
    {
        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var payloadHash = payload is null ? EmptyPayloadHash : ToHex(SHA256.HashData(payload));

        var uri = request.RequestUri!;
        request.Headers.Host = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);

        // Canonical headers must be lower-cased and sorted by name.
        var canonicalHeaders = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["host"] = request.Headers.Host!,
            ["x-amz-content-sha256"] = payloadHash,
            ["x-amz-date"] = amzDate
        };

        if (request.Headers.TryGetValues("x-amz-acl", out var acl))
            canonicalHeaders["x-amz-acl"] = string.Join(",", acl).Trim();

        if (request.Content?.Headers.ContentType is { } contentType)
            canonicalHeaders["content-type"] = contentType.ToString();

        var signedHeaders = string.Join(";", canonicalHeaders.Keys);
        var canonicalHeaderBlock = string.Concat(canonicalHeaders.Select(h => $"{h.Key}:{h.Value}\n"));

        var canonicalRequest = string.Join('\n',
            request.Method.Method,
            uri.AbsolutePath,
            uri.Query.TrimStart('?'),
            canonicalHeaderBlock,
            signedHeaders,
            payloadHash);

        var credentialScope = $"{dateStamp}/{_region}/{Service}/aws4_request";
        var stringToSign = string.Join('\n',
            Algorithm,
            amzDate,
            credentialScope,
            ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest))));

        var signingKey = DeriveSigningKey(dateStamp);
        var signature = ToHex(HmacSha256(signingKey, stringToSign));

        request.Headers.TryAddWithoutValidation("Authorization",
            $"{Algorithm} Credential={_accessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}");
    }

    private byte[] DeriveSigningKey(string dateStamp)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes($"AWS4{_secretKey}"), dateStamp);
        var kRegion = HmacSha256(kDate, _region);
        var kService = HmacSha256(kRegion, Service);
        return HmacSha256(kService, "aws4_request");
    }

    private static byte[] HmacSha256(byte[] key, string data) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(data));

    private static string ToHex(byte[] bytes) => Convert.ToHexStringLower(bytes);

    /// <summary>S3 wants each path segment percent-encoded, but the slashes left intact.</summary>
    private static string EncodePath(string key) =>
        string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
}

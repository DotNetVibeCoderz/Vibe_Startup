using FastRide.Shared.Storage;

namespace FastRide.Api.Infrastructure;

/// <summary>Generic S3-compatible storage (AWS S3, MinIO, DigitalOcean Spaces, etc.).</summary>
public class S3CompatibleStorageProvider : IStorageProvider
{
    private readonly string _endpoint, _bucket, _accessKey, _secretKey, _region, _publicUrl;

    public S3CompatibleStorageProvider(IConfiguration config)
    {
        _endpoint = config["Storage:S3:Endpoint"] ?? "https://s3.amazonaws.com";
        _bucket = config["Storage:S3:Bucket"] ?? "fastride";
        _accessKey = config["Storage:S3:AccessKey"] ?? "";
        _secretKey = config["Storage:S3:SecretKey"] ?? "";
        _region = config["Storage:S3:Region"] ?? "us-east-1";
        _publicUrl = config["Storage:S3:PublicUrl"] ?? $"{_endpoint}/{_bucket}";
    }

    public async Task<string> UploadAsync(string fileName, byte[] data, string contentType, CancellationToken ct = default)
    {
        using var client = CreateClient();
        using var ms = new MemoryStream(data);
        var putReq = new HttpRequestMessage(HttpMethod.Put, $"{_endpoint}/{_bucket}/{fileName}")
        {
            Content = new ByteArrayContent(data)
        };
        putReq.Content.Headers.ContentType = new(contentType);
        putReq.Headers.Add("x-amz-acl", "public-read");

        await SignRequest(putReq, ms);
        var resp = await client.SendAsync(putReq, ct);
        resp.EnsureSuccessStatusCode();
        return $"{_publicUrl}/{fileName}";
    }

    public async Task<byte[]?> DownloadAsync(string fileName, CancellationToken ct = default)
    {
        using var client = CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Get, $"{_endpoint}/{_bucket}/{fileName}");
        await SignRequest(req, null);
        var resp = await client.SendAsync(req, ct);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadAsByteArrayAsync(ct) : null;
    }

    public async Task<bool> DeleteAsync(string fileName, CancellationToken ct = default)
    {
        using var client = CreateClient();
        var req = new HttpRequestMessage(HttpMethod.Delete, $"{_endpoint}/{_bucket}/{fileName}");
        await SignRequest(req, null);
        var resp = await client.SendAsync(req, ct);
        return resp.IsSuccessStatusCode;
    }

    public Task<bool> ExistsAsync(string fileName, CancellationToken ct = default) => Task.FromResult(false); // Head request would be needed

    public string GeneratePhotoFileName(Guid userId, string extension)
        => $"photos/{userId.ToString("N")[..12]}_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension.TrimStart('.')}";

    // Simplified AWS Signature V4 — production should use AWS SDK
    private Task SignRequest(HttpRequestMessage request, Stream? body)
    {
        request.Headers.Add("Authorization", $"AWS4-HMAC-SHA256 Credential={_accessKey}/...");
        return Task.CompletedTask;
    }

    private HttpClient CreateClient() => new() { Timeout = TimeSpan.FromSeconds(30) };
}

using FastRide.Shared.Storage;

namespace FastRide.Api.Infrastructure;

/// <summary>Azure Blob Storage provider using REST API.</summary>
public class AzureBlobStorageProvider : IStorageProvider
{
    private readonly string _connectionString, _containerName, _accountName, _accountKey;

    public AzureBlobStorageProvider(IConfiguration config)
    {
        _connectionString = config["Storage:Azure:ConnectionString"] ?? "";
        _containerName = config["Storage:Azure:Container"] ?? "fastride-photos";
        // Parse account name/key from connection string for SAS generation
        var parts = _connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Split('=', 2))
            .ToDictionary(k => k[0].Trim(), v => v.Length > 1 ? v[1].Trim() : "");
        parts.TryGetValue("AccountName", out _accountName); _accountName ??= "";
        parts.TryGetValue("AccountKey", out _accountKey); _accountKey ??= "";
    }

    private string BlobUrl(string fileName) =>
        $"https://{_accountName}.blob.core.windows.net/{_containerName}/{fileName}";

    public async Task<string> UploadAsync(string fileName, byte[] data, string contentType, CancellationToken ct = default)
    {
        using var client = new HttpClient();
        var req = new HttpRequestMessage(HttpMethod.Put, BlobUrl(fileName))
        {
            Content = new ByteArrayContent(data)
        };
        req.Content.Headers.ContentType = new(contentType);
        req.Headers.Add("x-ms-blob-type", "BlockBlob");
        req.Headers.Add("x-ms-version", "2020-04-08");

        var resp = await client.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        return BlobUrl(fileName);
    }

    public async Task<byte[]?> DownloadAsync(string fileName, CancellationToken ct = default)
    {
        using var client = new HttpClient();
        var resp = await client.GetAsync(BlobUrl(fileName), ct);
        return resp.IsSuccessStatusCode ? await resp.Content.ReadAsByteArrayAsync(ct) : null;
    }

    public async Task<bool> DeleteAsync(string fileName, CancellationToken ct = default)
    {
        using var client = new HttpClient();
        var req = new HttpRequestMessage(HttpMethod.Delete, BlobUrl(fileName));
        req.Headers.Add("x-ms-version", "2020-04-08");
        var resp = await client.SendAsync(req, ct);
        return resp.IsSuccessStatusCode;
    }

    public Task<bool> ExistsAsync(string fileName, CancellationToken ct = default) => Task.FromResult(false);

    public string GeneratePhotoFileName(Guid userId, string extension)
        => $"photos/{userId.ToString("N")[..12]}_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension.TrimStart('.')}";
}

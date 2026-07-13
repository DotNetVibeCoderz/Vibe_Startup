using Microsoft.Extensions.Configuration;
using PCHub.Shared.Interfaces;

namespace PCHub.Shared.Services;

public class StorageMultiProviderService : IStorageService
{
    private readonly IStorageService _provider;

    public StorageMultiProviderService(IConfiguration? config = null)
    {
        var provider = config?["Storage:Provider"] ?? "FileSystem";
        var basePath = config?["Storage:BasePath"] ?? "uploads";
        _provider = provider.ToLower() switch
        {
            "azureblob" => new AzureBlobStorageService(config),
            "s3" => new S3StorageService(config),
            _ => new StorageService(basePath) // MinIO juga fallback ke FileSystem
        };
    }

    public Task<string> UploadFileAsync(string name, byte[] data, string mime) => _provider.UploadFileAsync(name, data, mime);
    public Task<byte[]> DownloadFileAsync(string path) => _provider.DownloadFileAsync(path);
    public Task<bool> DeleteFileAsync(string path) => _provider.DeleteFileAsync(path);
}

internal class AzureBlobStorageService : IStorageService
{
    private readonly string? _c; private readonly string _ctr;
    public AzureBlobStorageService(IConfiguration? config) { _c = config?["Storage:AzureBlob:ConnectionString"]; _ctr = config?["Storage:AzureBlob:ContainerName"] ?? "pchub-uploads"; }
    public async Task<string> UploadFileAsync(string name, byte[] data, string mime)
    {
        if (string.IsNullOrEmpty(_c)) throw new InvalidOperationException("Azure not configured");
        var blob = new Azure.Storage.Blobs.BlobServiceClient(_c);
        var cc = blob.GetBlobContainerClient(_ctr); await cc.CreateIfNotExistsAsync();
        var bc = cc.GetBlobClient($"{Guid.NewGuid():N}_{name}");
        using var s = new MemoryStream(data);
        await bc.UploadAsync(s, new Azure.Storage.Blobs.Models.BlobUploadOptions { HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = mime } });
        return bc.Uri.ToString();
    }
    public Task<byte[]> DownloadFileAsync(string p) => Task.FromResult(Array.Empty<byte>());
    public Task<bool> DeleteFileAsync(string p) => Task.FromResult(true);
}

internal class S3StorageService : IStorageService
{
    private readonly string? _ak, _sk, _b, _r, _ep;
    public S3StorageService(IConfiguration? config) { _ak = config?["Storage:S3:AccessKey"]; _sk = config?["Storage:S3:SecretKey"]; _b = config?["Storage:S3:BucketName"] ?? "pchub"; _r = config?["Storage:S3:Region"] ?? "ap-southeast-1"; _ep = config?["Storage:S3:Endpoint"]; }
    public async Task<string> UploadFileAsync(string name, byte[] data, string mime)
    {
        if (string.IsNullOrEmpty(_ak)) throw new InvalidOperationException("S3 not configured");
        var cfg = new Amazon.S3.AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_r) };
        if (!string.IsNullOrEmpty(_ep)) cfg.ServiceURL = _ep;
        using var c = new Amazon.S3.AmazonS3Client(_ak, _sk, cfg);
        using var s = new MemoryStream(data);
        var k = $"{Guid.NewGuid():N}_{name}";
        await c.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest { BucketName = _b, Key = k, InputStream = s, ContentType = mime });
        return $"s3://{_b}/{k}";
    }
    public Task<byte[]> DownloadFileAsync(string p) => Task.FromResult(Array.Empty<byte>());
    public Task<bool> DeleteFileAsync(string p) => Task.FromResult(true);
}

using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;

namespace AppBender.Core.Services;

/// <summary>
/// Pluggable blob storage. Provider selected via "Storage:Provider":
/// FileSystem (default) | S3 (also MinIO via ServiceUrl) | AzureBlob.
/// </summary>
public interface IStorageService
{
    Task<string> SaveAsync(string path, Stream content, string? contentType = null);
    Task<Stream?> OpenReadAsync(string path);
    Task<string?> ReadTextAsync(string path);
    Task WriteTextAsync(string path, string content);
    Task DeleteAsync(string path);
    Task<List<string>> ListAsync(string prefix);
    /// <summary>Public URL for content saved under the web root (FileSystem provider), otherwise a provider URL.</summary>
    string GetPublicUrl(string path);
}

public class StorageService : IStorageService
{
    private readonly string _provider;
    private readonly IConfiguration _config;
    private readonly string _basePath;
    private readonly Lazy<IAmazonS3> _s3;
    private readonly Lazy<BlobContainerClient> _blob;

    public StorageService(IConfiguration config)
    {
        _config = config;
        _provider = (config["Storage:Provider"] ?? "FileSystem").ToLowerInvariant();
        var basePath = config["Storage:FileSystem:BasePath"];
        _basePath = string.IsNullOrWhiteSpace(basePath)
            ? Path.Combine(AppContext.BaseDirectory, "storage")
            : basePath;
        _s3 = new Lazy<IAmazonS3>(() =>
        {
            var s3Config = new AmazonS3Config { ForcePathStyle = true };
            var serviceUrl = config["Storage:S3:ServiceUrl"];
            if (!string.IsNullOrEmpty(serviceUrl)) s3Config.ServiceURL = serviceUrl; // MinIO/custom endpoint
            else s3Config.AuthenticationRegion = config["Storage:S3:Region"] ?? "us-east-1";
            return new AmazonS3Client(config["Storage:S3:AccessKey"], config["Storage:S3:SecretKey"], s3Config);
        });
        _blob = new Lazy<BlobContainerClient>(() =>
            new BlobContainerClient(config["Storage:AzureBlob:ConnectionString"],
                config["Storage:AzureBlob:Container"] ?? "appbender"));
    }

    private string Bucket => _config["Storage:S3:Bucket"] ?? "appbender";

    private string FullPath(string path)
    {
        var full = Path.GetFullPath(Path.Combine(_basePath, path.Replace('\\', '/').TrimStart('/')));
        if (!full.StartsWith(Path.GetFullPath(_basePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path escapes the storage root.");
        return full;
    }

    public async Task<string> SaveAsync(string path, Stream content, string? contentType = null)
    {
        switch (_provider)
        {
            case "s3":
                await _s3.Value.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = Bucket, Key = path, InputStream = content,
                    ContentType = contentType ?? "application/octet-stream"
                });
                return path;
            case "azureblob":
                await _blob.Value.CreateIfNotExistsAsync();
                await _blob.Value.GetBlobClient(path).UploadAsync(content, overwrite: true);
                return path;
            default:
                var full = FullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                await using (var file = File.Create(full))
                    await content.CopyToAsync(file);
                return path;
        }
    }

    public async Task<Stream?> OpenReadAsync(string path)
    {
        switch (_provider)
        {
            case "s3":
                try
                {
                    var response = await _s3.Value.GetObjectAsync(Bucket, path);
                    var ms = new MemoryStream();
                    await response.ResponseStream.CopyToAsync(ms);
                    ms.Position = 0;
                    return ms;
                }
                catch (AmazonS3Exception) { return null; }
            case "azureblob":
                var client = _blob.Value.GetBlobClient(path);
                if (!await client.ExistsAsync()) return null;
                var stream = new MemoryStream();
                await client.DownloadToAsync(stream);
                stream.Position = 0;
                return stream;
            default:
                var full = FullPath(path);
                return File.Exists(full) ? File.OpenRead(full) : null;
        }
    }

    public async Task<string?> ReadTextAsync(string path)
    {
        await using var stream = await OpenReadAsync(path);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    public async Task WriteTextAsync(string path, string content)
    {
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        await SaveAsync(path, ms, "text/plain");
    }

    public async Task DeleteAsync(string path)
    {
        switch (_provider)
        {
            case "s3": await _s3.Value.DeleteObjectAsync(Bucket, path); break;
            case "azureblob": await _blob.Value.DeleteBlobIfExistsAsync(path); break;
            default:
                var full = FullPath(path);
                if (File.Exists(full)) File.Delete(full);
                break;
        }
    }

    public async Task<List<string>> ListAsync(string prefix)
    {
        switch (_provider)
        {
            case "s3":
                var response = await _s3.Value.ListObjectsV2Async(new ListObjectsV2Request { BucketName = Bucket, Prefix = prefix });
                return response.S3Objects?.Select(o => o.Key).ToList() ?? [];
            case "azureblob":
                var results = new List<string>();
                await foreach (var blob in _blob.Value.GetBlobsAsync(
                    Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, prefix,
                    CancellationToken.None))
                    results.Add(blob.Name);
                return results;
            default:
                var dir = FullPath(prefix);
                if (!Directory.Exists(dir)) return [];
                return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(_basePath, f).Replace('\\', '/'))
                    .ToList();
        }
    }

    public string GetPublicUrl(string path) => _provider switch
    {
        "s3" => $"{_config["Storage:S3:ServiceUrl"]?.TrimEnd('/')}/{Bucket}/{path}",
        "azureblob" => $"{_blob.Value.Uri}/{path}",
        _ => $"/files/{path.TrimStart('/')}"
    };
}

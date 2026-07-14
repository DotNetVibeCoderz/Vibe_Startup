using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;

namespace BlazorViz.Services.Storage;

/// <summary>Bound from the "Storage" section: Provider = FileSystem | AzureBlob | S3 (S3 covers MinIO).</summary>
public sealed class StorageOptions
{
    public const string Section = "Storage";
    public string Provider { get; set; } = "FileSystem";
    /// <summary>FileSystem root (relative to content root when not absolute).</summary>
    public string Root { get; set; } = "App_Data/files";
    public string? AzureConnectionString { get; set; }
    public string AzureContainer { get; set; } = "blazorviz";
    public string? S3AccessKey { get; set; }
    public string? S3SecretKey { get; set; }
    public string S3Bucket { get; set; } = "blazorviz";
    /// <summary>Set to e.g. http://localhost:9000 for MinIO.</summary>
    public string? S3ServiceUrl { get; set; }
    public string S3Region { get; set; } = "us-east-1";
}

public interface IFileStorage
{
    string Provider { get; }
    Task<string> SaveAsync(string path, Stream content, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string path, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
    Task<List<string>> ListAsync(string prefix, CancellationToken ct = default);
}

public sealed class FileSystemStorage(string root) : IFileStorage
{
    public string Provider => "FileSystem";

    private string Resolve(string path)
    {
        var full = Path.GetFullPath(Path.Combine(root, path.Replace('\\', '/').TrimStart('/')));
        if (!full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path escapes the storage root.");
        return full;
    }

    public async Task<string> SaveAsync(string path, Stream content, CancellationToken ct = default)
    {
        var full = Resolve(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var fs = File.Create(full);
        await content.CopyToAsync(fs, ct);
        return full;
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default) =>
        Task.FromResult<Stream>(File.OpenRead(Resolve(path)));

    public Task DeleteAsync(string path, CancellationToken ct = default)
    {
        var full = Resolve(path);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    public Task<List<string>> ListAsync(string prefix, CancellationToken ct = default)
    {
        var dir = Resolve(prefix);
        return Task.FromResult(Directory.Exists(dir)
            ? Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/')).ToList()
            : []);
    }
}

public sealed class AzureBlobStorage(StorageOptions opts) : IFileStorage
{
    public string Provider => "AzureBlob";

    private BlobContainerClient Container()
    {
        var client = new BlobContainerClient(opts.AzureConnectionString, opts.AzureContainer);
        client.CreateIfNotExists();
        return client;
    }

    public async Task<string> SaveAsync(string path, Stream content, CancellationToken ct = default)
    {
        var blob = Container().GetBlobClient(path);
        await blob.UploadAsync(content, overwrite: true, ct);
        return blob.Uri.ToString();
    }

    public async Task<Stream> OpenReadAsync(string path, CancellationToken ct = default) =>
        await Container().GetBlobClient(path).OpenReadAsync(cancellationToken: ct);

    public async Task DeleteAsync(string path, CancellationToken ct = default) =>
        await Container().GetBlobClient(path).DeleteIfExistsAsync(cancellationToken: ct);

    public async Task<List<string>> ListAsync(string prefix, CancellationToken ct = default)
    {
        var result = new List<string>();
        await foreach (var blob in Container().GetBlobsAsync(
            Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, prefix, ct))
            result.Add(blob.Name);
        return result;
    }
}

/// <summary>Amazon S3 / MinIO (set S3ServiceUrl for MinIO or any S3-compatible endpoint).</summary>
public sealed class S3Storage(StorageOptions opts) : IFileStorage, IDisposable
{
    public string Provider => "S3";

    private readonly AmazonS3Client _client = new(
        opts.S3AccessKey, opts.S3SecretKey,
        new AmazonS3Config
        {
            ServiceURL = string.IsNullOrWhiteSpace(opts.S3ServiceUrl) ? null : opts.S3ServiceUrl,
            ForcePathStyle = !string.IsNullOrWhiteSpace(opts.S3ServiceUrl),
            AuthenticationRegion = opts.S3Region
        });

    public async Task<string> SaveAsync(string path, Stream content, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = opts.S3Bucket,
            Key = path,
            InputStream = ms
        }, ct);
        return $"s3://{opts.S3Bucket}/{path}";
    }

    public async Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
    {
        var res = await _client.GetObjectAsync(opts.S3Bucket, path, ct);
        return res.ResponseStream;
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default) =>
        await _client.DeleteObjectAsync(opts.S3Bucket, path, ct);

    public async Task<List<string>> ListAsync(string prefix, CancellationToken ct = default)
    {
        var res = await _client.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = opts.S3Bucket,
            Prefix = prefix
        }, ct);
        return res.S3Objects?.Select(o => o.Key).ToList() ?? [];
    }

    public void Dispose() => _client.Dispose();
}

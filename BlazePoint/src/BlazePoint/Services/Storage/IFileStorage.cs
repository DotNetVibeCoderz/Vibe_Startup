namespace BlazePoint.Services.Storage;

/// <summary>Abstraction over physical file storage (FileSystem, Azure Blob, S3, MinIO).</summary>
public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream> OpenReadAsync(string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}

public class FileSystemStorage(IConfiguration config, IWebHostEnvironment env) : IFileStorage
{
    private readonly string _root = System.IO.Path.IsPathRooted(config["Storage:FileSystem:RootPath"] ?? "App_Data/files")
        ? config["Storage:FileSystem:RootPath"]!
        : System.IO.Path.Combine(env.ContentRootPath, config["Storage:FileSystem:RootPath"] ?? "App_Data/files");

    private string PathFor(string key)
    {
        var safe = key.Replace('\\', '/').TrimStart('/');
        if (safe.Contains("..")) throw new InvalidOperationException("Invalid storage key.");
        return System.IO.Path.Combine(_root, safe.Replace('/', System.IO.Path.DirectorySeparatorChar));
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
        => Task.FromResult<Stream>(File.OpenRead(PathFor(key)));

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = PathFor(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => Task.FromResult(File.Exists(PathFor(key)));
}

public class AzureBlobStorage : IFileStorage
{
    private readonly Azure.Storage.Blobs.BlobContainerClient _container;

    public AzureBlobStorage(IConfiguration config)
    {
        var cs = config["Storage:AzureBlob:ConnectionString"]
                 ?? throw new InvalidOperationException("Storage:AzureBlob:ConnectionString not configured.");
        _container = new Azure.Storage.Blobs.BlobContainerClient(cs, config["Storage:AzureBlob:Container"] ?? "blazepoint");
        _container.CreateIfNotExists();
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        => await _container.GetBlobClient(key).UploadAsync(content, overwrite: true, ct);

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
        => await _container.GetBlobClient(key).OpenReadAsync(cancellationToken: ct);

    public async Task DeleteAsync(string key, CancellationToken ct = default)
        => await _container.GetBlobClient(key).DeleteIfExistsAsync(cancellationToken: ct);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => await _container.GetBlobClient(key).ExistsAsync(ct);
}

/// <summary>S3-compatible storage; also used for MinIO via custom ServiceURL.</summary>
public class S3Storage : IFileStorage
{
    private readonly Amazon.S3.IAmazonS3 _client;
    private readonly string _bucket;

    public S3Storage(IConfiguration config, bool minio)
    {
        var section = minio ? "Storage:MinIO" : "Storage:S3";
        _bucket = config[$"{section}:Bucket"] ?? "blazepoint";
        var s3Config = new Amazon.S3.AmazonS3Config();
        if (minio)
        {
            s3Config.ServiceURL = config[$"{section}:Endpoint"] ?? "http://localhost:9000";
            s3Config.ForcePathStyle = true;
        }
        else
        {
            s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(config[$"{section}:Region"] ?? "us-east-1");
        }
        _client = new Amazon.S3.AmazonS3Client(config[$"{section}:AccessKey"], config[$"{section}:SecretKey"], s3Config);
    }

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;
        await _client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
        {
            BucketName = _bucket, Key = key, InputStream = ms, ContentType = contentType
        }, ct);
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken ct = default)
    {
        var response = await _client.GetObjectAsync(_bucket, key, ct);
        return response.ResponseStream;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
        => await _client.DeleteObjectAsync(_bucket, key, ct);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try { await _client.GetObjectMetadataAsync(_bucket, key, ct); return true; }
        catch (Amazon.S3.AmazonS3Exception) { return false; }
    }
}

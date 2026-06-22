using Minio;
using Minio.DataModel.Args;

namespace JuraganKost.Services.Storage;

/// <summary>
/// MinIO storage provider - self-hosted S3-compatible object storage.
/// Uses the Minio .NET SDK for operations.
/// </summary>
public class MinIOStorageProvider : IStorageProvider
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;
    private readonly string _endpoint;
    private readonly bool _useSsl;

    public MinIOStorageProvider(IConfiguration config)
    {
        var minioConfig = config.GetSection("StorageConfig:MinIO");
        _endpoint = minioConfig.GetValue<string>("Endpoint") ?? "localhost:9000";
        var accessKey = minioConfig.GetValue<string>("AccessKey") ?? "minioadmin";
        var secretKey = minioConfig.GetValue<string>("SecretKey") ?? "minioadmin";
        _bucketName = minioConfig.GetValue<string>("BucketName") ?? "juragankost";
        _useSsl = minioConfig.GetValue<bool>("UseSsl");

        _minioClient = new MinioClient()
            .WithEndpoint(_endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(_useSsl)
            .Build();

        // Ensure bucket exists
        EnsureBucketAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureBucketAsync()
    {
        bool found = await _minioClient.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_bucketName));
        if (!found)
        {
            await _minioClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_bucketName));
        }
    }

    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        var ext = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid():N}{ext}";

        var putArgs = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(uniqueName)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(putArgs);

        var protocol = _useSsl ? "https" : "http";
        return $"{protocol}://{_endpoint}/{_bucketName}/{uniqueName}";
    }

    public async Task<bool> DeleteAsync(string fileKey)
    {
        var key = ExtractKey(fileKey);
        try
        {
            await _minioClient.RemoveObjectAsync(
                new RemoveObjectArgs().WithBucket(_bucketName).WithObject(key));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Stream?> DownloadAsync(string fileKey)
    {
        var key = ExtractKey(fileKey);
        var ms = new MemoryStream();
        try
        {
            await _minioClient.GetObjectAsync(
                new GetObjectArgs().WithBucket(_bucketName).WithObject(key)
                    .WithCallbackStream(stream => stream.CopyTo(ms)));
            ms.Position = 0;
            return ms;
        }
        catch
        {
            return null;
        }
    }

    public string GetPublicUrl(string fileKey) => fileKey;

    public async Task<bool> ExistsAsync(string fileKey)
    {
        var key = ExtractKey(fileKey);
        try
        {
            await _minioClient.StatObjectAsync(
                new StatObjectArgs().WithBucket(_bucketName).WithObject(key));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> ListAsync(string? prefix = null)
    {
        var results = new List<string>();
        var protocol = _useSsl ? "https" : "http";
        var listArgs = new ListObjectsArgs().WithBucket(_bucketName);
        if (!string.IsNullOrEmpty(prefix)) listArgs = listArgs.WithPrefix(prefix);

        await foreach (var item in _minioClient.ListObjectsEnumAsync(listArgs))
        {
            results.Add($"{protocol}://{_endpoint}/{_bucketName}/{item.Key}");
        }
        return results;
    }

    private string ExtractKey(string fileKey)
    {
        try
        {
            var uri = new Uri(fileKey);
            // Format: http(s)://endpoint/bucket/key
            var segments = uri.AbsolutePath.TrimStart('/').Split('/', 2);
            return segments.Length > 1 ? segments[1] : segments[0];
        }
        catch
        {
            return fileKey;
        }
    }
}

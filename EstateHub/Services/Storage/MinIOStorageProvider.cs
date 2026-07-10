using Minio;
using Minio.DataModel.Args;

namespace EstateHub.Services.Storage;

/// <summary>
/// MinIO object storage provider (S3-compatible, self-hosted)
/// Configure via appsettings: Storage:MinIO:Endpoint, AccessKey, SecretKey, BucketName
/// </summary>
public class MinIOStorageProvider : IStorageProvider
{
    private readonly IMinioClient _client;
    private readonly string _bucketName;
    private readonly string _endpoint;

    public string ProviderName => "MinIO";

    public MinIOStorageProvider(IConfiguration config)
    {
        _endpoint = config.GetValue<string>("Storage:MinIO:Endpoint") ?? "localhost:9000";
        var accessKey = config.GetValue<string>("Storage:MinIO:AccessKey") ?? "minioadmin";
        var secretKey = config.GetValue<string>("Storage:MinIO:SecretKey") ?? "minioadmin";
        _bucketName = config.GetValue<string>("Storage:MinIO:BucketName") ?? "estatehub";

        _client = new MinioClient()
            .WithEndpoint(_endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(false)
            .Build();

        // Ensure bucket exists
        try
        {
            var found = _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucketName)).GetAwaiter().GetResult();
            if (!found)
                _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucketName)).GetAwaiter().GetResult();
        }
        catch { /* will retry on first use */ }
    }

    public async Task<StorageResult> UploadAsync(Stream stream, string fileName, string contentType, string subfolder = "")
    {
        try
        {
            var objectName = string.IsNullOrEmpty(subfolder)
                ? $"{Guid.NewGuid():N}_{fileName}"
                : $"{subfolder}/{Guid.NewGuid():N}_{fileName}";

            // Ensure bucket exists
            var bucketExists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucketName));
            if (!bucketExists)
                await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucketName));

            var putArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName)
                .WithStreamData(stream)
                .WithObjectSize(stream.Length)
                .WithContentType(contentType);

            await _client.PutObjectAsync(putArgs);
            var url = $"http://{_endpoint}/{_bucketName}/{objectName}";
            return StorageResult.Ok(url, fileName, stream.Length);
        }
        catch (Exception ex)
        {
            return StorageResult.Fail($"MinIO upload failed: {ex.Message}");
        }
    }

    public async Task<StorageResult> UploadAsync(byte[] data, string fileName, string contentType, string subfolder = "")
    {
        using var stream = new MemoryStream(data);
        return await UploadAsync(stream, fileName, contentType, subfolder);
    }

    public async Task<bool> DeleteAsync(string fileUrl)
    {
        try
        {
            var objectName = ExtractObjectName(fileUrl);
            await _client.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName));
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> ExistsAsync(string fileUrl)
    {
        try
        {
            var objectName = ExtractObjectName(fileUrl);
            await _client.StatObjectAsync(new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName));
            return true;
        }
        catch { return false; }
    }

    public string GetPublicUrl(string fileName, string subfolder = "")
    {
        var objectName = string.IsNullOrEmpty(subfolder) ? fileName : $"{subfolder}/{fileName}";
        return $"http://{_endpoint}/{_bucketName}/{objectName}";
    }

    public async Task<Stream?> DownloadAsync(string fileUrl)
    {
        try
        {
            var objectName = ExtractObjectName(fileUrl);
            var stream = new MemoryStream();
            await _client.GetObjectAsync(new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName)
                .WithCallbackStream(s => s.CopyTo(stream)));
            stream.Position = 0;
            return stream;
        }
        catch { return null; }
    }

    public async Task<List<string>> ListFilesAsync(string subfolder = "", int maxResults = 100)
    {
        try
        {
            var files = new List<string>();
            var args = new ListObjectsArgs()
                .WithBucket(_bucketName)
                .WithPrefix(string.IsNullOrEmpty(subfolder) ? "" : $"{subfolder}/")
                .WithRecursive(false);

            await foreach (var item in _client.ListObjectsEnumAsync(args))
            {
                files.Add($"http://{_endpoint}/{_bucketName}/{item.Key}");
                if (files.Count >= maxResults) break;
            }
            return files;
        }
        catch { return new List<string>(); }
    }

    private string ExtractObjectName(string url)
    {
        var prefix = $"http://{_endpoint}/{_bucketName}/";
        return url.StartsWith(prefix) ? url[prefix.Length..] : url.Split('/').Last();
    }
}

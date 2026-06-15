using Minio;
using Minio.DataModel.Args;

namespace SoccerWizard.Services.Storage;

/// <summary>
/// Storage backend: MinIO (self-hosted S3-compatible).
/// </summary>
public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _client;
    private readonly string _bucketName;
    public string ProviderName => "MinIO";

    public MinioStorageService(IConfiguration config)
    {
        _bucketName = config["Storage:MinIO:BucketName"] ?? "soccerwizard-uploads";
        var endpoint = config["Storage:MinIO:Endpoint"] ?? "localhost:9000";
        var accessKey = config["Storage:MinIO:AccessKey"] ?? "minioadmin";
        var secretKey = config["Storage:MinIO:SecretKey"] ?? "minioadmin";
        var useSsl = bool.TryParse(config["Storage:MinIO:UseSsl"], out var ssl) && ssl;

        _client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();

        // Ensure bucket exists
        Task.Run(async () => await EnsureBucketAsync()).Wait();
    }

    private async Task EnsureBucketAsync()
    {
        try
        {
            bool found = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_bucketName));
            if (!found)
                await _client.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_bucketName));
        }
        catch { /* bucket creation can fail silently for read-only access */ }
    }

    public async Task<string> UploadAsync(string fileName, Stream stream, string contentType)
    {
        var objectName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{Path.GetExtension(fileName)}";

        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType));

        return objectName;
    }

    public async Task<Stream?> DownloadAsync(string fileName)
    {
        try
        {
            var ms = new MemoryStream();
            await _client.GetObjectAsync(new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(fileName)
                .WithCallbackStream(s => s.CopyTo(ms)));
            ms.Position = 0;
            return ms;
        }
        catch { return null; }
    }

    public async Task DeleteAsync(string fileName)
    {
        await _client.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(fileName));
    }

    public async Task<bool> ExistsAsync(string fileName)
    {
        try
        {
            await _client.StatObjectAsync(new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(fileName));
            return true;
        }
        catch { return false; }
    }

    public string GetPublicUrl(string fileName)
        => $"/api/storage/{fileName}"; // Proxy endpoint
}

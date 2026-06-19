using Minio;
using Minio.DataModel.Args;

namespace Comblang.Services.Storage;

/// <summary>
/// MinIO object-storage provider (S3-compatible). Ideal for self-hosted or
/// on-premises deployments. Uses the official Minio .NET SDK.
/// </summary>
public class MinioStorageProvider : IStorageProvider
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucket;
    private readonly string _publicUrlPrefix;

    /// <summary>
    /// Creates a MinIO storage provider.
    /// </summary>
    /// <param name="endpoint">MinIO server endpoint (e.g. localhost:9000).</param>
    /// <param name="accessKey">MinIO access key.</param>
    /// <param name="secretKey">MinIO secret key.</param>
    /// <param name="bucket">Bucket name.</param>
    /// <param name="useSsl">Whether to connect via HTTPS (default: false).</param>
    /// <param name="publicUrlPrefix">
    /// Public base URL (e.g. https://cdn.example.com). Defaults to the
    /// MinIO endpoint.
    /// </param>
    public MinioStorageProvider(
        string endpoint,
        string accessKey,
        string secretKey,
        string bucket,
        bool useSsl = false,
        string? publicUrlPrefix = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentNullException(nameof(endpoint));
        if (string.IsNullOrWhiteSpace(bucket))
            throw new ArgumentNullException(nameof(bucket));

        _bucket = bucket;
        _minioClient = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();

        // The bucket will be created lazily on first upload; we could also
        // ensure it exists here with:
        // _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket)).Wait();

        _publicUrlPrefix = !string.IsNullOrWhiteSpace(publicUrlPrefix)
            ? publicUrlPrefix.TrimEnd('/')
            : $"{(useSsl ? "https" : "http")}://{endpoint}/{bucket}";
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        // Ensure bucket exists (idempotent)
        var bucketExists = await _minioClient.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_bucket));

        if (!bucketExists)
        {
            await _minioClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_bucket));
        }

        if (content.CanSeek)
            content.Position = 0;

        var putArgs = new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(fileName)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(putArgs);

        return $"{_publicUrlPrefix}/{fileName}";
    }

    /// <inheritdoc />
    public async Task<Stream?> DownloadAsync(string fileName)
    {
        try
        {
            var memoryStream = new MemoryStream();
            var getArgs = new GetObjectArgs()
                .WithBucket(_bucket)
                .WithObject(fileName)
                .WithCallbackStream(stream =>
                {
                    stream.CopyTo(memoryStream);
                    memoryStream.Position = 0;
                });

            await _minioClient.GetObjectAsync(getArgs);
            return memoryStream;
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string fileName)
    {
        try
        {
            var removeArgs = new RemoveObjectArgs()
                .WithBucket(_bucket)
                .WithObject(fileName);

            await _minioClient.RemoveObjectAsync(removeArgs);
            return true;
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task<string> GetPublicUrlAsync(string fileName)
    {
        var url = $"{_publicUrlPrefix}/{fileName}";
        return Task.FromResult(url);
    }
}

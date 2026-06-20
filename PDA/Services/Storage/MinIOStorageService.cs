using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace PDA.Services.Storage;

/// <summary>
/// MinIO Storage implementation.
/// MinIO is an S3-compatible, open-source object storage.
/// Uses the Minio .NET SDK for operations.
/// </summary>
public class MinIOStorageService : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;
    private readonly ILogger<MinIOStorageService> _logger;

    public MinIOStorageService(IConfiguration configuration, ILogger<MinIOStorageService> logger)
    {
        _logger = logger;
        _bucketName = configuration["Storage:MinIO:BucketName"] ?? "pda-uploads";

        var endpoint = configuration["Storage:MinIO:Endpoint"] ?? "localhost:9000";
        var accessKey = configuration["Storage:MinIO:AccessKey"] ?? "minioadmin";
        var secretKey = configuration["Storage:MinIO:SecretKey"] ?? "minioadmin";

        _minioClient = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(false) // Set to true for HTTPS in production
            .Build();

        // Ensure bucket exists
        EnsureBucketExistsAsync().GetAwaiter().GetResult();
        _logger.LogInformation("MinIO Storage initialized: endpoint={Endpoint}, bucket={Bucket}", endpoint, _bucketName);
    }

    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            var found = await _minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_bucketName));

            if (!found)
            {
                await _minioClient.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_bucketName));
                _logger.LogInformation("Created MinIO bucket: {Bucket}", _bucketName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create/verify MinIO bucket: {Bucket}", _bucketName);
        }
    }

    /// <summary>
    /// Upload a file to MinIO
    /// </summary>
    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        try
        {
            var uniqueName = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(fileName)}";

            var args = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(uniqueName)
                .WithStreamData(content)
                .WithObjectSize(content.Length)
                .WithContentType(contentType);

            await _minioClient.PutObjectAsync(args);
            _logger.LogInformation("File uploaded to MinIO: {FileName} -> {Object}", fileName, uniqueName);

            return uniqueName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload to MinIO: {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Download a file from MinIO
    /// </summary>
    public async Task<Stream?> DownloadAsync(string filePath)
    {
        try
        {
            var memoryStream = new MemoryStream();

            var args = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream));

            await _minioClient.GetObjectAsync(args);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (ObjectNotFoundException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download from MinIO: {Path}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Delete a file from MinIO
    /// </summary>
    public async Task<bool> DeleteAsync(string filePath)
    {
        try
        {
            var args = new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath);

            await _minioClient.RemoveObjectAsync(args);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete from MinIO: {Path}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Get a pre-signed URL for temporary public access (24 hours)
    /// </summary>
    public async Task<string> GetPublicUrlAsync(string filePath)
    {
        try
        {
            var args = new PresignedGetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath)
                .WithExpiry(24 * 60 * 60); // 24 hours in seconds

            var url = await _minioClient.PresignedGetObjectAsync(args);
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate pre-signed URL: {Path}", filePath);
            return string.Empty;
        }
    }

    /// <summary>
    /// Check if a file exists in MinIO
    /// </summary>
    public async Task<bool> ExistsAsync(string filePath)
    {
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath);

            await _minioClient.StatObjectAsync(args);
            return true;
        }
        catch (ObjectNotFoundException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }
}

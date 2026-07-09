using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Minio;
using Minio.DataModel.Args;

namespace FuelStation.Services;

// ============================================================================
// INTERFACE
// ============================================================================

/// <summary>
/// Multi-provider storage abstraction.
/// Supports: FileSystem (default), Azure Blob, AWS S3, MinIO.
/// </summary>
public interface IStorageService
{
    /// <summary>Upload file stream, returns the storage path/key.</summary>
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType);

    /// <summary>Download file as stream from path/key.</summary>
    Task<Stream?> DownloadAsync(string filePath);

    /// <summary>Delete file by path/key.</summary>
    Task<bool> DeleteAsync(string filePath);

    /// <summary>Get public/accessible URL for the file.</summary>
    string GetPublicUrl(string filePath);

    /// <summary>Check if file exists.</summary>
    Task<bool> ExistsAsync(string filePath);

    /// <summary>Get file metadata (size in bytes, last modified).</summary>
    Task<(long Size, DateTimeOffset? LastModified)?> GetMetadataAsync(string filePath);
}

// ============================================================================
// 1. FILE SYSTEM STORAGE
// ============================================================================

/// <summary>
/// Local file system storage. Stores files under wwwroot/{basePath}/.
/// </summary>
public class FileSystemStorageService : IStorageService
{
    private readonly string _basePath;
    private readonly string _rootPath;
    private readonly ILogger<FileSystemStorageService> _logger;

    public FileSystemStorageService(IConfiguration config, IWebHostEnvironment env, ILogger<FileSystemStorageService> logger)
    {
        _logger = logger;
        _basePath = config.GetValue<string>("Storage:BasePath", "uploads");
        _rootPath = env.WebRootPath ?? env.ContentRootPath;
        var fullPath = Path.Combine(_rootPath, _basePath);
        if (!Directory.Exists(fullPath))
            Directory.CreateDirectory(fullPath);
        _logger.LogInformation("FileSystem storage initialized at: {Path}", fullPath);
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
    {
        var uniqueName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N[..8]}_{fileName}";
        var relativePath = Path.Combine(_basePath, uniqueName);
        var fullPath = Path.Combine(_rootPath, relativePath);

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await fileStream.CopyToAsync(fs);

        _logger.LogDebug("File uploaded: {RelativePath}", relativePath);
        return relativePath;
    }

    public Task<Stream?> DownloadAsync(string filePath)
    {
        var fullPath = Path.Combine(_rootPath, filePath);
        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        // Open with read-sharing so it can be read concurrently
        return Task.FromResult<Stream?>(new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    public Task<bool> DeleteAsync(string filePath)
    {
        var fullPath = Path.Combine(_rootPath, filePath);
        if (!File.Exists(fullPath))
            return Task.FromResult(false);

        File.Delete(fullPath);
        _logger.LogDebug("File deleted: {RelativePath}", filePath);
        return Task.FromResult(true);
    }

    public string GetPublicUrl(string filePath)
    {
        return $"/{filePath.Replace("\\", "/")}";
    }

    public Task<bool> ExistsAsync(string filePath)
    {
        var fullPath = Path.Combine(_rootPath, filePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<(long Size, DateTimeOffset? LastModified)?> GetMetadataAsync(string filePath)
    {
        var fullPath = Path.Combine(_rootPath, filePath);
        if (!File.Exists(fullPath))
            return Task.FromResult<(long, DateTimeOffset?)?>(null);

        var fi = new FileInfo(fullPath);
        return Task.FromResult<(long, DateTimeOffset?)?>((fi.Length, fi.LastWriteTimeUtc));
    }
}

// ============================================================================
// 2. AZURE BLOB STORAGE
// ============================================================================

/// <summary>
/// Azure Blob Storage implementation.
/// Configuration: "Storage:ConnectionString" and "Storage:ContainerName".
/// </summary>
public class AzureBlobStorageService : IStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(IConfiguration config, ILogger<AzureBlobStorageService> logger)
    {
        _logger = logger;
        var connectionString = config.GetValue<string>("Storage:ConnectionString")
            ?? throw new InvalidOperationException("Azure Blob: ConnectionString is required.");
        _containerName = config.GetValue<string>("Storage:ContainerName", "fuelstation");

        _blobServiceClient = new BlobServiceClient(connectionString);

        // Ensure container exists
        var container = _blobServiceClient.GetBlobContainerClient(_containerName);
        container.CreateIfNotExists(PublicAccessType.Blob);

        _logger.LogInformation("Azure Blob storage initialized. Container: {Container}", _containerName);
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
    {
        var blobName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N[..8]}_{fileName}";
        var container = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blob = container.GetBlobClient(blobName);

        // Reset stream position
        if (fileStream.CanSeek)
            fileStream.Position = 0;

        await blob.UploadAsync(fileStream, new BlobHttpHeaders { ContentType = contentType });

        _logger.LogDebug("Azure Blob uploaded: {BlobName}", blobName);
        return blobName;
    }

    public async Task<Stream?> DownloadAsync(string filePath)
    {
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blob = container.GetBlobClient(filePath);

            if (!await blob.ExistsAsync())
                return null;

            var stream = new MemoryStream();
            await blob.DownloadToAsync(stream);
            stream.Position = 0;
            return stream;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(string filePath)
    {
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blob = container.GetBlobClient(filePath);
            return await blob.DeleteIfExistsAsync();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return false;
        }
    }

    public string GetPublicUrl(string filePath)
    {
        var container = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blob = container.GetBlobClient(filePath);

        // Generate SAS URL valid for 24 hours (for private containers)
        // For public containers, just return the blob URI directly
        try
        {
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _containerName,
                BlobName = filePath,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(24)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            // Note: SAS generation requires storage account key in connection string
            var sasUri = blob.GenerateSasUri(sasBuilder);
            return sasUri.ToString();
        }
        catch
        {
            // Fallback: return direct URI (works for public containers)
            return blob.Uri.ToString();
        }
    }

    public async Task<bool> ExistsAsync(string filePath)
    {
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blob = container.GetBlobClient(filePath);
            return await blob.ExistsAsync();
        }
        catch
        {
            return false;
        }
    }

    public async Task<(long Size, DateTimeOffset? LastModified)?> GetMetadataAsync(string filePath)
    {
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blob = container.GetBlobClient(filePath);
            var props = await blob.GetPropertiesAsync();
            if (props?.Value == null) return null;
            return (props.Value.ContentLength, props.Value.LastModified);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}

// ============================================================================
// 3. AWS S3 STORAGE
// ============================================================================

/// <summary>
/// AWS S3 storage implementation.
/// Configuration: "Storage:AccessKey", "Storage:SecretKey", "Storage:BucketName",
///                "Storage:Region" (optional, default us-east-1), "Storage:Endpoint" (optional).
/// </summary>
public class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string? _serviceUrl; // For generating public URLs
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(IConfiguration config, ILogger<S3StorageService> logger)
    {
        _logger = logger;
        _bucketName = config.GetValue<string>("Storage:BucketName", "fuelstation");
        var accessKey = config.GetValue<string>("Storage:AccessKey");
        var secretKey = config.GetValue<string>("Storage:SecretKey");
        var region = config.GetValue<string>("Storage:Region", "us-east-1");
        var endpoint = config.GetValue<string>("Storage:Endpoint");

        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("AWS S3: AccessKey and SecretKey are required.");

        var s3Config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
            ForcePathStyle = !string.IsNullOrEmpty(endpoint) // Required for MinIO/S3-compatible
        };

        if (!string.IsNullOrEmpty(endpoint))
        {
            _serviceUrl = endpoint.TrimEnd('/');
            s3Config.ServiceURL = _serviceUrl;
        }

        _s3Client = new AmazonS3Client(
            new BasicAWSCredentials(accessKey, secretKey),
            s3Config
        );

        // Ensure bucket exists
        EnsureBucketExistsAsync().GetAwaiter().GetResult();

        _logger.LogInformation("AWS S3 storage initialized. Bucket: {Bucket}, Region: {Region}", _bucketName, region);
    }

    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            var exists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _bucketName);
            if (!exists)
            {
                await _s3Client.PutBucketAsync(_bucketName);
                _logger.LogInformation("S3 bucket created: {Bucket}", _bucketName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not verify/create S3 bucket: {Bucket}", _bucketName);
        }
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
    {
        var key = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N[..8]}_{fileName}";

        if (fileStream.CanSeek)
            fileStream.Position = 0;

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = fileStream,
            ContentType = contentType,
            AutoCloseStream = false
        };

        await _s3Client.PutObjectAsync(request);

        _logger.LogDebug("S3 object uploaded: {Key}", key);
        return key;
    }

    public async Task<Stream?> DownloadAsync(string filePath)
    {
        try
        {
            var response = await _s3Client.GetObjectAsync(_bucketName, filePath);
            var stream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(stream);
            stream.Position = 0;
            response.Dispose();
            return stream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(string filePath)
    {
        try
        {
            await _s3Client.DeleteObjectAsync(_bucketName, filePath);
            _logger.LogDebug("S3 object deleted: {Key}", filePath);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public string GetPublicUrl(string filePath)
    {
        // Generate a pre-signed URL valid for 24 hours
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = filePath,
                Expires = DateTime.UtcNow.AddHours(24),
                Protocol = Protocol.HTTPS
            };
            return _s3Client.GetPreSignedURL(request);
        }
        catch
        {
            // Fallback: construct regional URL
            var region = _s3Client.Config.RegionEndpoint?.SystemName ?? "us-east-1";
            return _serviceUrl != null
                ? $"{_serviceUrl}/{_bucketName}/{filePath}"
                : $"https://{_bucketName}.s3.{region}.amazonaws.com/{filePath}";
        }
    }

    public async Task<bool> ExistsAsync(string filePath)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(_bucketName, filePath);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<(long Size, DateTimeOffset? LastModified)?> GetMetadataAsync(string filePath)
    {
        try
        {
            var meta = await _s3Client.GetObjectMetadataAsync(_bucketName, filePath);
            return (meta.ContentLength, meta.LastModified);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}

// ============================================================================
// 4. MINIO STORAGE
// ============================================================================

/// <summary>
/// MinIO storage implementation (S3-compatible).
/// Configuration: "Storage:Endpoint", "Storage:AccessKey", "Storage:SecretKey",
///                "Storage:BucketName", "Storage:Region" (optional).
/// </summary>
public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;
    private readonly string _endpoint;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(IConfiguration config, ILogger<MinioStorageService> logger)
    {
        _logger = logger;
        _bucketName = config.GetValue<string>("Storage:BucketName", "fuelstation");
        _endpoint = config.GetValue<string>("Storage:Endpoint")
            ?? throw new InvalidOperationException("MinIO: Endpoint is required.");
        var accessKey = config.GetValue<string>("Storage:AccessKey")
            ?? throw new InvalidOperationException("MinIO: AccessKey is required.");
        var secretKey = config.GetValue<string>("Storage:SecretKey")
            ?? throw new InvalidOperationException("MinIO: SecretKey is required.");
        var region = config.GetValue<string>("Storage:Region", "us-east-1");
        var useSsl = config.GetValue<bool>("Storage:UseSsl", false);

        _minioClient = new MinioClient()
            .WithEndpoint(_endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithRegion(region)
            .WithSSL(useSsl)
            .Build();

        // Ensure bucket (fire-and-forget to not block startup)
        _ = EnsureBucketExistsAsync();

        _logger.LogInformation("MinIO storage initialized. Endpoint: {Endpoint}, Bucket: {Bucket}", _endpoint, _bucketName);
    }

    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            var exists = await _minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_bucketName));

            if (!exists)
            {
                await _minioClient.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_bucketName));
                _logger.LogInformation("MinIO bucket created: {Bucket}", _bucketName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not verify/create MinIO bucket: {Bucket}", _bucketName);
        }
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
    {
        var objectName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N[..8]}_{fileName}";

        if (fileStream.CanSeek)
            fileStream.Position = 0;

        var args = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(fileStream)
            .WithObjectSize(fileStream.Length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(args);

        _logger.LogDebug("MinIO object uploaded: {Object}", objectName);
        return objectName;
    }

    public async Task<Stream?> DownloadAsync(string filePath)
    {
        try
        {
            var stream = new MemoryStream();
            var args = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath)
                .WithCallbackStream(s =>
                {
                    s.CopyTo(stream);
                    stream.Position = 0;
                });

            await _minioClient.GetObjectAsync(args);
            stream.Position = 0;
            return stream.Length > 0 ? stream : null;
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MinIO download error: {Key}", filePath);
            return null;
        }
    }

    public async Task<bool> DeleteAsync(string filePath)
    {
        try
        {
            var args = new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath);

            await _minioClient.RemoveObjectAsync(args);
            _logger.LogDebug("MinIO object deleted: {Object}", filePath);
            return true;
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return false;
        }
    }

    public string GetPublicUrl(string filePath)
    {
        // Generate a pre-signed URL valid for 24 hours
        try
        {
            var args = new PresignedGetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath)
                .WithExpiry(24 * 60 * 60); // 24 hours in seconds

            var url = _minioClient.PresignedGetObjectAsync(args).GetAwaiter().GetResult();
            return url;
        }
        catch
        {
            // Fallback: construct path-style URL
            var protocol = _endpoint.StartsWith("https://") ? "https" : "http";
            var host = _endpoint.Replace("https://", "").Replace("http://", "").TrimEnd('/');
            return $"{protocol}://{host}/{_bucketName}/{filePath}";
        }
    }

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
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return false;
        }
    }

    public async Task<(long Size, DateTimeOffset? LastModified)?> GetMetadataAsync(string filePath)
    {
        try
        {
            var args = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath);

            var stat = await _minioClient.StatObjectAsync(args);
            return (stat.Size, stat.LastModified);
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return null;
        }
    }
}

// ============================================================================
// REGISTRATION
// ============================================================================

/// <summary>
/// Registers the appropriate IStorageService implementation based on configuration.
/// Usage in Program.cs: builder.Services.AddStorageService(builder.Configuration);
/// </summary>
public static class StorageServiceRegistration
{
    public static IServiceCollection AddStorageService(this IServiceCollection services, IConfiguration config)
    {
        var provider = config.GetValue<string>("Storage:Provider", "FileSystem");

        switch (provider.ToLowerInvariant())
        {
            case "azureblob":
            case "azure":
                services.AddSingleton<IStorageService, AzureBlobStorageService>();
                break;

            case "s3":
            case "aws":
            case "awss3":
                services.AddSingleton<IStorageService, S3StorageService>();
                break;

            case "minio":
                services.AddSingleton<IStorageService, MinioStorageService>();
                break;

            case "filesystem":
            case "file":
            default:
                services.AddSingleton<IStorageService, FileSystemStorageService>();
                break;
        }

        return services;
    }
}

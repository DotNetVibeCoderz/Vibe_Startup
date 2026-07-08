using Azure.Storage.Blobs;
using Amazon.S3;
using Amazon.S3.Model;
using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Options;

namespace PadelHub.Services;

/// <summary>
/// Multi-provider file storage service supporting:
/// - FileSystem (default, development)
/// - Azure Blob Storage
/// - AWS S3
/// - MinIO (S3-compatible, self-hosted)
/// 
/// Configured via appsettings.json → "StorageProvider" key.
/// </summary>
public class FileStorageService
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FileStorageService> _logger;
    private readonly string _provider;
    private readonly string _baseUrl;

    // Lazy clients - only created when needed
    private BlobServiceClient? _blobServiceClient;
    private IAmazonS3? _s3Client;
    private IMinioClient? _minioClient;

    public FileStorageService(IConfiguration config, IWebHostEnvironment env, ILogger<FileStorageService> logger)
    {
        _config = config;
        _env = env;
        _logger = logger;
        _provider = config.GetValue<string>("StorageProvider") ?? "FileSystem";
        _baseUrl = config.GetValue<string>("AppSettings:AppUrl") ?? "";
    }

    /// <summary>
    /// Save file to configured storage provider. Returns public URL.
    /// </summary>
    public async Task<string> SaveFileAsync(IFormFile file, string folder = "uploads")
    {
        return _provider switch
        {
            "AzureBlob" => await SaveToAzureBlobAsync(file, folder),
            "S3" => await SaveToS3Async(file, folder),
            "MinIO" => await SaveToMinIOAsync(file, folder),
            _ => await SaveToFileSystemAsync(file, folder)
        };
    }

    /// <summary>
    /// Save base64 image to storage. Returns public URL.
    /// </summary>
    public async Task<string> SaveBase64ImageAsync(string base64Data, string folder = "uploads")
    {
        var data = base64Data.Contains(',') ? base64Data.Split(',')[1] : base64Data;
        var bytes = Convert.FromBase64String(data);
        var fileName = $"{Guid.NewGuid()}.png";
        var contentType = "image/png";

        return _provider switch
        {
            "AzureBlob" => await SaveBytesToAzureBlobAsync(bytes, fileName, contentType, folder),
            "S3" => await SaveBytesToS3Async(bytes, fileName, contentType, folder),
            "MinIO" => await SaveBytesToMinIOAsync(bytes, fileName, contentType, folder),
            _ => await SaveBytesToFileSystemAsync(bytes, fileName, folder)
        };
    }

    /// <summary>
    /// Delete file from storage.
    /// </summary>
    public async Task DeleteFileAsync(string relativeUrl)
    {
        switch (_provider)
        {
            case "AzureBlob":
                await DeleteFromAzureBlobAsync(relativeUrl);
                break;
            case "S3":
                await DeleteFromS3Async(relativeUrl);
                break;
            case "MinIO":
                await DeleteFromMinIOAsync(relativeUrl);
                break;
            default:
                DeleteFromFileSystem(relativeUrl);
                break;
        }
    }

    /// <summary>
    /// Get a pre-signed download URL (valid for specified duration).
    /// Useful for private buckets or temporary access.
    /// </summary>
    public async Task<string> GetPresignedUrlAsync(string objectKey, TimeSpan? expiry = null)
    {
        expiry ??= TimeSpan.FromHours(1);

        return _provider switch
        {
            "S3" => await GetS3PresignedUrlAsync(objectKey, expiry.Value),
            "MinIO" => await GetMinIOPresignedUrlAsync(objectKey, expiry.Value),
            _ => objectKey // FileSystem: just return path
        };
    }

    // ================================================================
    // FILESYSTEM IMPLEMENTATION
    // ================================================================

    private async Task<string> SaveToFileSystemAsync(IFormFile file, string folder)
    {
        var uploadsPath = Path.Combine(_env.WebRootPath, folder);
        Directory.CreateDirectory(uploadsPath);

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsPath, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        _logger.LogInformation("File saved to FileSystem: {Path}", $"/{folder}/{fileName}");
        return $"/{folder}/{fileName}";
    }

    private async Task<string> SaveBytesToFileSystemAsync(byte[] bytes, string fileName, string folder)
    {
        var uploadsPath = Path.Combine(_env.WebRootPath, folder);
        Directory.CreateDirectory(uploadsPath);
        var filePath = Path.Combine(uploadsPath, fileName);
        await File.WriteAllBytesAsync(filePath, bytes);
        _logger.LogInformation("Bytes saved to FileSystem: {Path}", $"/{folder}/{fileName}");
        return $"/{folder}/{fileName}";
    }

    private void DeleteFromFileSystem(string relativePath)
    {
        var fullPath = Path.Combine(_env.WebRootPath, relativePath.TrimStart('/'));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("File deleted from FileSystem: {Path}", fullPath);
        }
    }

    // ================================================================
    // AZURE BLOB STORAGE IMPLEMENTATION
    // ================================================================

    private BlobServiceClient GetAzureBlobClient()
    {
        if (_blobServiceClient == null)
        {
            var connStr = _config["Storage:AzureBlob:ConnectionString"];
            if (string.IsNullOrEmpty(connStr))
                throw new InvalidOperationException("Azure Blob Storage connection string not configured.");
            _blobServiceClient = new BlobServiceClient(connStr);
        }
        return _blobServiceClient;
    }

    private async Task<string> SaveToAzureBlobAsync(IFormFile file, string folder)
    {
        var containerName = _config["Storage:AzureBlob:ContainerName"] ?? "padelhub";
        var blobClient = GetAzureBlobClient();
        var container = blobClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync();

        var fileName = $"{folder}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var blob = container.GetBlobClient(fileName);

        await using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, new Azure.Storage.Blobs.Models.BlobUploadOptions
        {
            HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders
            {
                ContentType = file.ContentType
            }
        });

        _logger.LogInformation("File saved to Azure Blob: {BlobName}", fileName);
        return blob.Uri.ToString(); // Full public URL
    }

    private async Task<string> SaveBytesToAzureBlobAsync(byte[] bytes, string fileName, string contentType, string folder)
    {
        var containerName = _config["Storage:AzureBlob:ContainerName"] ?? "padelhub";
        var blobClient = GetAzureBlobClient();
        var container = blobClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync();

        var blobName = $"{folder}/{fileName}";
        var blob = container.GetBlobClient(blobName);

        await using var stream = new MemoryStream(bytes);
        await blob.UploadAsync(stream, new Azure.Storage.Blobs.Models.BlobUploadOptions
        {
            HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType }
        });

        _logger.LogInformation("Bytes saved to Azure Blob: {BlobName}", blobName);
        return blob.Uri.ToString();
    }

    private async Task DeleteFromAzureBlobAsync(string url)
    {
        var containerName = _config["Storage:AzureBlob:ContainerName"] ?? "padelhub";
        var blobClient = GetAzureBlobClient();
        var container = blobClient.GetBlobContainerClient(containerName);

        // Extract blob name from URL
        var uri = new Uri(url);
        var blobName = string.Join("", uri.Segments.SkipWhile(s => !s.Contains(containerName)).Skip(1));
        blobName = Uri.UnescapeDataString(blobName);

        var blob = container.GetBlobClient(blobName);
        await blob.DeleteIfExistsAsync();
        _logger.LogInformation("File deleted from Azure Blob: {BlobName}", blobName);
    }

    // ================================================================
    // AWS S3 IMPLEMENTATION
    // ================================================================

    private IAmazonS3 GetS3Client()
    {
        if (_s3Client == null)
        {
            var accessKey = _config["Storage:S3:AccessKey"];
            var secretKey = _config["Storage:S3:SecretKey"];
            var region = _config["Storage:S3:Region"] ?? "us-east-1";
            var serviceUrl = _config["Storage:S3:ServiceUrl"]; // Optional: for MinIO or custom endpoints

            if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
                throw new InvalidOperationException("AWS S3 access key/secret not configured.");

            var config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region),
                ForcePathStyle = !string.IsNullOrEmpty(serviceUrl), // Required for MinIO
            };

            if (!string.IsNullOrEmpty(serviceUrl))
                config.ServiceURL = serviceUrl;

            _s3Client = new AmazonS3Client(accessKey, secretKey, config);
        }
        return _s3Client;
    }

    private async Task<string> SaveToS3Async(IFormFile file, string folder)
    {
        var bucketName = _config["Storage:S3:BucketName"] ?? "padelhub";
        var s3 = GetS3Client();

        // Ensure bucket exists
        if (!await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(s3, bucketName))
            await s3.PutBucketAsync(bucketName);

        var fileName = $"{folder}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

        await using var stream = file.OpenReadStream();
        var putRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = fileName,
            InputStream = stream,
            ContentType = file.ContentType
        };

        await s3.PutObjectAsync(putRequest);

        _logger.LogInformation("File saved to S3: {Key}", fileName);

        // Return the S3 URL
        var serviceUrl = _config["Storage:S3:ServiceUrl"];
        if (!string.IsNullOrEmpty(serviceUrl))
            return $"{serviceUrl}/{bucketName}/{fileName}";

        var region = _config["Storage:S3:Region"] ?? "us-east-1";
        return $"https://{bucketName}.s3.{region}.amazonaws.com/{fileName}";
    }

    private async Task<string> SaveBytesToS3Async(byte[] bytes, string fileName, string contentType, string folder)
    {
        var bucketName = _config["Storage:S3:BucketName"] ?? "padelhub";
        var s3 = GetS3Client();

        var key = $"{folder}/{fileName}";
        await using var stream = new MemoryStream(bytes);

        var putRequest = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType
        };

        await s3.PutObjectAsync(putRequest);
        _logger.LogInformation("Bytes saved to S3: {Key}", key);

        var serviceUrl = _config["Storage:S3:ServiceUrl"];
        if (!string.IsNullOrEmpty(serviceUrl))
            return $"{serviceUrl}/{bucketName}/{key}";

        var region = _config["Storage:S3:Region"] ?? "us-east-1";
        return $"https://{bucketName}.s3.{region}.amazonaws.com/{key}";
    }

    private async Task DeleteFromS3Async(string url)
    {
        var bucketName = _config["Storage:S3:BucketName"] ?? "padelhub";
        var s3 = GetS3Client();

        // Extract key from URL
        var key = ExtractS3KeyFromUrl(url, bucketName);

        await s3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = bucketName,
            Key = key
        });

        _logger.LogInformation("File deleted from S3: {Key}", key);
    }

    private async Task<string> GetS3PresignedUrlAsync(string key, TimeSpan expiry)
    {
        var bucketName = _config["Storage:S3:BucketName"] ?? "padelhub";
        var s3 = GetS3Client();

        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiry)
        };

        return await Task.FromResult(s3.GetPreSignedURL(request));
    }

    // ================================================================
    // MINIO IMPLEMENTATION
    // ================================================================

    private IMinioClient GetMinIOClient()
    {
        if (_minioClient == null)
        {
            var endpoint = _config["Storage:MinIO:Endpoint"] ?? "localhost:9000";
            var accessKey = _config["Storage:MinIO:AccessKey"] ?? "minioadmin";
            var secretKey = _config["Storage:MinIO:SecretKey"] ?? "minioadmin";

            _minioClient = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .WithSSL(false)
                .Build();
        }
        return _minioClient;
    }

    private async Task<string> SaveToMinIOAsync(IFormFile file, string folder)
    {
        var bucketName = _config["Storage:MinIO:BucketName"] ?? "padelhub";
        var minio = GetMinIOClient();

        // Ensure bucket exists
        var bucketExists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName));
        if (!bucketExists)
            await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));

        var fileName = $"{folder}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

        await using var stream = file.OpenReadStream();
        var putArgs = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(fileName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(file.ContentType);

        await minio.PutObjectAsync(putArgs);

        _logger.LogInformation("File saved to MinIO: {Object}", fileName);

        var endpoint = _config["Storage:MinIO:Endpoint"] ?? "localhost:9000";
        return $"http://{endpoint}/{bucketName}/{fileName}";
    }

    private async Task<string> SaveBytesToMinIOAsync(byte[] bytes, string fileName, string contentType, string folder)
    {
        var bucketName = _config["Storage:MinIO:BucketName"] ?? "padelhub";
        var minio = GetMinIOClient();

        var key = $"{folder}/{fileName}";
        await using var stream = new MemoryStream(bytes);

        var putArgs = new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(key)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType);

        await minio.PutObjectAsync(putArgs);
        _logger.LogInformation("Bytes saved to MinIO: {Object}", key);

        var endpoint = _config["Storage:MinIO:Endpoint"] ?? "localhost:9000";
        return $"http://{endpoint}/{bucketName}/{key}";
    }

    private async Task DeleteFromMinIOAsync(string url)
    {
        var bucketName = _config["Storage:MinIO:BucketName"] ?? "padelhub";
        var minio = GetMinIOClient();

        var key = ExtractS3KeyFromUrl(url, bucketName);

        var removeArgs = new RemoveObjectArgs()
            .WithBucket(bucketName)
            .WithObject(key);

        await minio.RemoveObjectAsync(removeArgs);
        _logger.LogInformation("File deleted from MinIO: {Object}", key);
    }

    private async Task<string> GetMinIOPresignedUrlAsync(string key, TimeSpan expiry)
    {
        var bucketName = _config["Storage:MinIO:BucketName"] ?? "padelhub";
        var minio = GetMinIOClient();

        var presignedArgs = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(key)
            .WithExpiry((int)expiry.TotalSeconds);

        return await minio.PresignedGetObjectAsync(presignedArgs);
    }

    // ================================================================
    // HELPERS
    // ================================================================

    /// <summary>
    /// Extracts S3/MinIO object key from a full URL.
    /// Handles both path-style (https://host/bucket/key) and
    /// virtual-hosted-style (https://bucket.s3.region.amazonaws.com/key).
    /// </summary>
    private static string ExtractS3KeyFromUrl(string url, string bucketName)
    {
        var uri = new Uri(url);

        // Path-style: http://localhost:9000/bucketName/key
        // Virtual-hosted-style: https://bucketName.s3.region.amazonaws.com/key
        if (uri.AbsolutePath.StartsWith($"/{bucketName}/"))
        {
            return Uri.UnescapeDataString(uri.AbsolutePath[($"/{bucketName}/").Length..]);
        }

        // Fallback: just return path without leading slash
        return Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
    }
}

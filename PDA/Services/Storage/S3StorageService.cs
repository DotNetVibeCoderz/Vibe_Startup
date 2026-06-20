using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace PDA.Services.Storage;

/// <summary>
/// AWS S3 Storage implementation.
/// Stores files in S3 buckets with pre-signed URL access.
/// </summary>
public class S3StorageService : IStorageService
{
    private readonly AmazonS3Client _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<S3StorageService> _logger;

    public S3StorageService(IConfiguration configuration, ILogger<S3StorageService> logger)
    {
        _logger = logger;
        _bucketName = configuration["Storage:S3:BucketName"] 
            ?? throw new InvalidOperationException("S3:BucketName not configured");

        var accessKey = configuration["Storage:S3:AccessKey"] ?? "";
        var secretKey = configuration["Storage:S3:SecretKey"] ?? "";
        var endpoint = configuration["Storage:S3:Endpoint"] ?? "";
        var region = configuration["Storage:S3:Region"] ?? "us-east-1";

        if (!string.IsNullOrEmpty(endpoint))
        {
            // Custom endpoint (for S3-compatible services)
            var config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true // Required for MinIO and most S3-compatible services
            };
            _s3Client = new AmazonS3Client(accessKey, secretKey, config);
        }
        else
        {
            // Standard AWS S3
            var config = new AmazonS3Config { RegionEndpoint = RegionEndpoint.GetBySystemName(region) };
            _s3Client = new AmazonS3Client(accessKey, secretKey, config);
        }

        // Ensure bucket exists
        EnsureBucketExistsAsync().GetAwaiter().GetResult();
        _logger.LogInformation("S3 Storage initialized: bucket={Bucket}", _bucketName);
    }

    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            var buckets = await _s3Client.ListBucketsAsync();
            if (!buckets.Buckets.Any(b => b.BucketName == _bucketName))
            {
                await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = _bucketName });
                _logger.LogInformation("Created S3 bucket: {Bucket}", _bucketName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create/verify S3 bucket: {Bucket}", _bucketName);
        }
    }

    /// <summary>
    /// Upload a file to S3
    /// </summary>
    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        try
        {
            var uniqueName = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(fileName)}";

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = uniqueName,
                InputStream = content,
                ContentType = contentType
            };

            await _s3Client.PutObjectAsync(request);
            _logger.LogInformation("File uploaded to S3: {FileName} -> {Key}", fileName, uniqueName);

            return uniqueName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload to S3: {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Download a file from S3
    /// </summary>
    public async Task<Stream?> DownloadAsync(string filePath)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = filePath
            };

            var response = await _s3Client.GetObjectAsync(request);
            var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download from S3: {Path}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Delete a file from S3
    /// </summary>
    public async Task<bool> DeleteAsync(string filePath)
    {
        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = filePath
            };

            await _s3Client.DeleteObjectAsync(request);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete from S3: {Path}", filePath);
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
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = filePath,
                Expires = DateTime.UtcNow.AddHours(24),
                Verb = HttpVerb.GET
            };

            return await Task.FromResult(_s3Client.GetPreSignedURL(request));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate pre-signed URL: {Path}", filePath);
            return string.Empty;
        }
    }

    /// <summary>
    /// Check if a file exists in S3
    /// </summary>
    public async Task<bool> ExistsAsync(string filePath)
    {
        try
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = filePath
            };

            await _s3Client.GetObjectMetadataAsync(request);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }
}

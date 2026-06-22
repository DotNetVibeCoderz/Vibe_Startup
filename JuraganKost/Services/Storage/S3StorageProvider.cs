using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;

namespace JuraganKost.Services.Storage;

/// <summary>
/// AWS S3 storage provider.
/// Also compatible with S3-compatible services including MinIO when ServiceUrl is set.
/// </summary>
public class S3StorageProvider : IStorageProvider
{
    private readonly AmazonS3Client _s3Client;
    private readonly string _bucketName;
    private readonly string _region;
    private readonly string? _serviceUrl;

    public S3StorageProvider(IConfiguration config)
    {
        var s3Config = config.GetSection("StorageConfig:S3");
        var accessKey = s3Config.GetValue<string>("AccessKey") ?? "";
        var secretKey = s3Config.GetValue<string>("SecretKey") ?? "";
        _bucketName = s3Config.GetValue<string>("BucketName") ?? "juragankost";
        _region = s3Config.GetValue<string>("Region") ?? "ap-southeast-1";
        _serviceUrl = s3Config.GetValue<string>("ServiceUrl");

        if (string.IsNullOrEmpty(accessKey))
            throw new InvalidOperationException("S3 AccessKey is not configured.");

        var s3ConfigObj = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_region),
            ForcePathStyle = !string.IsNullOrEmpty(_serviceUrl)
        };

        if (!string.IsNullOrEmpty(_serviceUrl))
            s3ConfigObj.ServiceURL = _serviceUrl;

        _s3Client = new AmazonS3Client(
            new BasicAWSCredentials(accessKey, secretKey),
            s3ConfigObj
        );

        EnsureBucketAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureBucketAsync()
    {
        try
        {
            var buckets = await _s3Client.ListBucketsAsync();
            if (!buckets.Buckets.Any(b => b.BucketName == _bucketName))
            {
                await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = _bucketName });
            }
        }
        catch
        {
            // Bucket might already exist or insufficient permissions
        }
    }

    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        var ext = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid():N}{ext}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = uniqueName,
            InputStream = content,
            ContentType = contentType
        };

        await _s3Client.PutObjectAsync(request);

        if (!string.IsNullOrEmpty(_serviceUrl))
            return $"{_serviceUrl}/{_bucketName}/{uniqueName}";

        return $"https://{_bucketName}.s3.{_region}.amazonaws.com/{uniqueName}";
    }

    public async Task<bool> DeleteAsync(string fileKey)
    {
        var key = ExtractKey(fileKey);
        var result = await _s3Client.DeleteObjectAsync(_bucketName, key);
        return result.HttpStatusCode == System.Net.HttpStatusCode.NoContent;
    }

    public async Task<Stream?> DownloadAsync(string fileKey)
    {
        var key = ExtractKey(fileKey);
        try
        {
            var response = await _s3Client.GetObjectAsync(_bucketName, key);
            var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms);
            ms.Position = 0;
            return ms;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
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
            await _s3Client.GetObjectMetadataAsync(_bucketName, key);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<List<string>> ListAsync(string? prefix = null)
    {
        var request = new ListObjectsV2Request { BucketName = _bucketName, Prefix = prefix };
        var response = await _s3Client.ListObjectsV2Async(request);
        return response.S3Objects
            .Select(o => GetPublicUrl($"https://{_bucketName}.s3.{_region}.amazonaws.com/{o.Key}"))
            .ToList();
    }

    private string ExtractKey(string fileKey)
    {
        try
        {
            var uri = new Uri(fileKey);
            var segments = uri.AbsolutePath.TrimStart('/').Split('/');
            if (!string.IsNullOrEmpty(_serviceUrl) && segments.Length > 1)
                return string.Join("/", segments.Skip(1));
            return string.Join("/", segments);
        }
        catch { return fileKey; }
    }
}

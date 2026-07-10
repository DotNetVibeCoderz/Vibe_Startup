using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace EstateHub.Services.Storage;

/// <summary>
/// AWS S3-compatible storage provider (also works with DigitalOcean Spaces, etc.)
/// Configure via appsettings: Storage:S3:AccessKey, SecretKey, BucketName, Region
/// </summary>
public class S3StorageProvider : IStorageProvider
{
    private readonly AmazonS3Client _client;
    private readonly string _bucketName;
    private readonly string _region;
    private readonly string? _customEndpoint; // For S3-compatible services like DigitalOcean

    public string ProviderName => "S3";

    public S3StorageProvider(IConfiguration config)
    {
        _bucketName = config.GetValue<string>("Storage:S3:BucketName") ?? "estatehub";
        _region = config.GetValue<string>("Storage:S3:Region") ?? "ap-southeast-1";
        var accessKey = config.GetValue<string>("Storage:S3:AccessKey") ?? "";
        var secretKey = config.GetValue<string>("Storage:S3:SecretKey") ?? "";
        _customEndpoint = config.GetValue<string>("Storage:S3:Endpoint");

        if (!string.IsNullOrEmpty(_customEndpoint))
        {
            var s3Config = new AmazonS3Config
            {
                ServiceURL = _customEndpoint,
                ForcePathStyle = true
            };
            _client = new AmazonS3Client(accessKey, secretKey, s3Config);
        }
        else
        {
            _client = new AmazonS3Client(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(_region));
        }

        // Ensure bucket exists
        try { _client.PutBucketAsync(_bucketName).GetAwaiter().GetResult(); }
        catch { /* bucket may already exist */ }
    }

    public async Task<StorageResult> UploadAsync(Stream stream, string fileName, string contentType, string subfolder = "")
    {
        try
        {
            var key = string.IsNullOrEmpty(subfolder)
                ? $"{Guid.NewGuid():N}_{fileName}"
                : $"{subfolder}/{Guid.NewGuid():N}_{fileName}";

            var uploadRequest = new TransferUtilityUploadRequest
            {
                InputStream = stream,
                Key = key,
                BucketName = _bucketName,
                ContentType = contentType
            };

            var transferUtility = new TransferUtility(_client);
            await transferUtility.UploadAsync(uploadRequest);

            var url = string.IsNullOrEmpty(_customEndpoint)
                ? $"https://{_bucketName}.s3.{_region}.amazonaws.com/{key}"
                : $"{_customEndpoint}/{_bucketName}/{key}";

            return StorageResult.Ok(url, fileName, stream.Length);
        }
        catch (Exception ex)
        {
            return StorageResult.Fail($"S3 upload failed: {ex.Message}");
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
            var key = ExtractKeyFromUrl(fileUrl);
            var response = await _client.DeleteObjectAsync(_bucketName, key);
            return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent;
        }
        catch { return false; }
    }

    public async Task<bool> ExistsAsync(string fileUrl)
    {
        try
        {
            var key = ExtractKeyFromUrl(fileUrl);
            await _client.GetObjectMetadataAsync(_bucketName, key);
            return true;
        }
        catch { return false; }
    }

    public string GetPublicUrl(string fileName, string subfolder = "")
    {
        var key = string.IsNullOrEmpty(subfolder) ? fileName : $"{subfolder}/{fileName}";
        return string.IsNullOrEmpty(_customEndpoint)
            ? $"https://{_bucketName}.s3.{_region}.amazonaws.com/{key}"
            : $"{_customEndpoint}/{_bucketName}/{key}";
    }

    public async Task<Stream?> DownloadAsync(string fileUrl)
    {
        try
        {
            var key = ExtractKeyFromUrl(fileUrl);
            var response = await _client.GetObjectAsync(_bucketName, key);
            var stream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(stream);
            stream.Position = 0;
            return stream;
        }
        catch { return null; }
    }

    public async Task<List<string>> ListFilesAsync(string subfolder = "", int maxResults = 100)
    {
        try
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = string.IsNullOrEmpty(subfolder) ? "" : $"{subfolder}/",
                MaxKeys = maxResults
            };
            var response = await _client.ListObjectsV2Async(request);
            return response.S3Objects
                .Select(o => GetPublicUrl(o.Key))
                .ToList();
        }
        catch { return new List<string>(); }
    }

    private string ExtractKeyFromUrl(string url)
    {
        if (string.IsNullOrEmpty(_customEndpoint))
        {
            var prefix = $"https://{_bucketName}.s3.{_region}.amazonaws.com/";
            return url.StartsWith(prefix) ? url[prefix.Length..] : url;
        }
        else
        {
            var prefix = $"{_customEndpoint}/{_bucketName}/";
            return url.Contains(prefix) ? url[(url.IndexOf(prefix) + prefix.Length)..] : url;
        }
    }
}

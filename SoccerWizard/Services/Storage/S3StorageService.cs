using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;

namespace SoccerWizard.Services.Storage;

/// <summary>
/// Storage backend: AWS S3.
/// </summary>
public class S3StorageService : IStorageService
{
    private readonly AmazonS3Client _client;
    private readonly string _bucketName;
    private readonly string _region;
    public string ProviderName => "S3";

    public S3StorageService(IConfiguration config)
    {
        _bucketName = config["Storage:S3:BucketName"] ?? "soccerwizard-uploads";
        _region = config["Storage:S3:Region"] ?? "us-east-1";

        var accessKey = config["Storage:S3:AccessKey"];
        var secretKey = config["Storage:S3:SecretKey"];
        var serviceUrl = config["Storage:S3:ServiceUrl"];

        if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
        {
            var credentials = new BasicAWSCredentials(accessKey, secretKey);
            var s3Config = new AmazonS3Config
            {
                RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_region),
                ForcePathStyle = true
            };
            if (!string.IsNullOrEmpty(serviceUrl))
                s3Config.ServiceURL = serviceUrl;

            _client = new AmazonS3Client(credentials, s3Config);
        }
        else
        {
            // Fallback: use default AWS credentials chain
            _client = new AmazonS3Client(Amazon.RegionEndpoint.GetBySystemName(_region));
        }
    }

    public async Task<string> UploadAsync(string fileName, Stream stream, string contentType)
    {
        var key = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{Path.GetExtension(fileName)}";

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType
        };

        await _client.PutObjectAsync(request);

        return $"https://{_bucketName}.s3.{_region}.amazonaws.com/{key}";
    }

    public async Task<Stream?> DownloadAsync(string fileName)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucketName, fileName);
            var ms = new MemoryStream();
            await response.ResponseStream.CopyToAsync(ms);
            ms.Position = 0;
            return ms;
        }
        catch { return null; }
    }

    public async Task DeleteAsync(string fileName)
    {
        await _client.DeleteObjectAsync(_bucketName, fileName);
    }

    public async Task<bool> ExistsAsync(string fileName)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucketName, fileName);
            return true;
        }
        catch { return false; }
    }

    public string GetPublicUrl(string fileName)
        => $"https://{_bucketName}.s3.{_region}.amazonaws.com/{fileName}";
}

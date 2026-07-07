using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;

namespace FitnessCenter.Services.Storage;

/// <summary>
/// AWS S3 (Simple Storage Service) provider.
/// Kompatibel dengan S3-compatible storage lainnya.
/// </summary>
public class S3StorageProvider : IStorageProvider, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _region;
    private readonly string _serviceUrl;
    private readonly string? _cdnEndpoint;
    private bool _disposed;

    public string ProviderName => "S3";

    public S3StorageProvider(IConfiguration config)
    {
        _bucketName = config.GetValue<string>("Storage:S3:BucketName") ?? "fitnesscenter";
        _region = config.GetValue<string>("Storage:S3:Region") ?? "us-east-1";
        _serviceUrl = config.GetValue<string>("Storage:S3:ServiceUrl") ?? string.Empty;
        _cdnEndpoint = config.GetValue<string>("Storage:S3:CdnEndpoint");

        var accessKey = config.GetValue<string>("Storage:S3:AccessKey") ?? "";
        var secretKey = config.GetValue<string>("Storage:S3:SecretKey") ?? "";

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
            throw new InvalidOperationException("S3 AccessKey and SecretKey must be configured.");

        var credentials = new BasicAWSCredentials(accessKey, secretKey);

        // Konfigurasi S3 client
        var s3Config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(_region),
            ForcePathStyle = !string.IsNullOrEmpty(_serviceUrl), // Path style untuk custom endpoint (MinIO compatible)
        };

        // Jika ada custom service URL (untuk endpoint non-AWS)
        if (!string.IsNullOrEmpty(_serviceUrl))
        {
            s3Config.ServiceURL = _serviceUrl;
        }

        _s3Client = new AmazonS3Client(credentials, s3Config);

        // Pastikan bucket exists
        EnsureBucketExistsAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            var exists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _bucketName);
            if (!exists)
            {
                var putBucketRequest = new PutBucketRequest
                {
                    BucketName = _bucketName,
                    UseClientRegion = true
                };
                await _s3Client.PutBucketAsync(putBucketRequest);

                // Set bucket policy untuk public read (opsional, tergantung kebutuhan)
                await SetPublicReadPolicyAsync();
            }
        }
        catch (Exception)
        {
            // Bucket mungkin sudah ada atau tidak bisa dibuat (pakai existing)
        }
    }

    private async Task SetPublicReadPolicyAsync()
    {
        try
        {
            var policy = $@"{{
                ""Version"": ""2012-10-17"",
                ""Statement"": [
                    {{
                        ""Sid"": ""PublicReadGetObject"",
                        ""Effect"": ""Allow"",
                        ""Principal"": ""*"",
                        ""Action"": ""s3:GetObject"",
                        ""Resource"": ""arn:aws:s3:::{_bucketName}/*""
                    }}
                ]
            }}";

            await _s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest
            {
                BucketName = _bucketName,
                Policy = policy
            });
        }
        catch
        {
            // Policy mungkin tidak bisa di-set di environment tertentu
        }
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string folder = "", string? contentType = null)
    {
        var extension = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid():N}_{SanitizeFileName(Path.GetFileNameWithoutExtension(fileName))}{extension}";
        var key = string.IsNullOrEmpty(folder) ? uniqueName : $"{folder}/{uniqueName}";

        if (fileStream.CanSeek) fileStream.Position = 0;

        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = fileStream,
            BucketName = _bucketName,
            Key = key,
            ContentType = contentType ?? GetContentType(fileName),
            AutoCloseStream = false,
        };

        var transferUtility = new TransferUtility(_s3Client);
        await transferUtility.UploadAsync(uploadRequest);

        return GetPublicUrl(uniqueName, folder);
    }

    public async Task<string> UploadBase64Async(string base64, string fileName, string folder = "")
    {
        var cleanBase64 = base64.Contains(',') ? base64[(base64.IndexOf(',') + 1)..] : base64;
        var bytes = Convert.FromBase64String(cleanBase64);
        using var ms = new MemoryStream(bytes);

        string? contentType = null;
        if (base64.Contains("data:"))
        {
            var prefix = base64[..base64.IndexOf(';')];
            contentType = prefix.Replace("data:", "");
        }

        return await UploadAsync(ms, fileName, folder, contentType);
    }

    public async Task<bool> DeleteAsync(string fileUrl)
    {
        try
        {
            var key = ExtractKey(fileUrl);
            if (string.IsNullOrEmpty(key)) return false;

            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            var response = await _s3Client.DeleteObjectAsync(deleteRequest);
            return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent
                || response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string fileUrl)
    {
        try
        {
            var key = ExtractKey(fileUrl);
            if (string.IsNullOrEmpty(key)) return false;

            var metadata = await _s3Client.GetObjectMetadataAsync(_bucketName, key);
            return metadata != null;
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

    public string GetPublicUrl(string fileKey, string folder)
    {
        var key = string.IsNullOrEmpty(folder) ? fileKey : $"{folder}/{fileKey}";

        // CDN endpoint jika tersedia
        if (!string.IsNullOrEmpty(_cdnEndpoint))
        {
            return $"{_cdnEndpoint.TrimEnd('/')}/{key}";
        }

        // Custom service URL
        if (!string.IsNullOrEmpty(_serviceUrl))
        {
            return $"{_serviceUrl.TrimEnd('/')}/{_bucketName}/{key}";
        }

        // Default S3 URL
        return $"https://{_bucketName}.s3.{_region}.amazonaws.com/{key}";
    }

    /// <summary>Ekstrak object key dari URL</summary>
    private string ExtractKey(string url)
    {
        try
        {
            var uri = new Uri(url);

            // Untuk custom endpoint: /bucket/key
            // Untuk AWS S3: /key atau bucket/key
            var path = uri.AbsolutePath.TrimStart('/');

            // Jika path dimulai dengan nama bucket, hapus
            if (path.StartsWith(_bucketName + "/", StringComparison.OrdinalIgnoreCase))
            {
                return path[(_bucketName.Length + 1)..];
            }

            return path;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        return fileName.Replace("\\", "/")
                       .Replace(" ", "_")
                       .Replace("#", "");
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            ".json" => "application/json",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _s3Client?.Dispose();
            _disposed = true;
        }
    }
}

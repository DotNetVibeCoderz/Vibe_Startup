using Minio;
using Minio.DataModel.Args;

namespace FitnessCenter.Services.Storage;

/// <summary>
/// MinIO Object Storage provider.
/// Open-source, S3-compatible, self-hosted storage.
/// </summary>
public class MinIOStorageProvider : IStorageProvider, IDisposable
{
    private readonly IMinioClient _minioClient;
    private readonly string _bucketName;
    private readonly string _endpoint;
    private bool _disposed;

    public string ProviderName => "MinIO";

    public MinIOStorageProvider(IConfiguration config)
    {
        _endpoint = config.GetValue<string>("Storage:MinIO:Endpoint") ?? "localhost:9000";
        _bucketName = config.GetValue<string>("Storage:MinIO:BucketName") ?? "fitnesscenter";
        var accessKey = config.GetValue<string>("Storage:MinIO:AccessKey") ?? "minioadmin";
        var secretKey = config.GetValue<string>("Storage:MinIO:SecretKey") ?? "minioadmin";
        var useSsl = config.GetValue<bool>("Storage:MinIO:UseSsl", false);

        _minioClient = new MinioClient()
            .WithEndpoint(_endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();

        // Pastikan bucket exists
        EnsureBucketExistsAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            var bucketExistsArgs = new BucketExistsArgs().WithBucket(_bucketName);
            var exists = await _minioClient.BucketExistsAsync(bucketExistsArgs);

            if (!exists)
            {
                var makeBucketArgs = new MakeBucketArgs().WithBucket(_bucketName);
                await _minioClient.MakeBucketAsync(makeBucketArgs);

                // Set bucket policy untuk public read
                await SetPublicPolicyAsync();
            }
        }
        catch (Exception)
        {
            // Bucket mungkin sudah ada, lanjutkan
        }
    }

    private async Task SetPublicPolicyAsync()
    {
        try
        {
            var policyJson = $@"{{
                ""Version"": ""2012-10-17"",
                ""Statement"": [
                    {{
                        ""Effect"": ""Allow"",
                        ""Principal"": {{""AWS"": [""*""]}},
                        ""Action"": [""s3:GetObject""],
                        ""Resource"": [""arn:aws:s3:::{_bucketName}/*""]
                    }}
                ]
            }}";

            var setPolicyArgs = new SetPolicyArgs()
                .WithBucket(_bucketName)
                .WithPolicy(policyJson);

            await _minioClient.SetPolicyAsync(setPolicyArgs);
        }
        catch
        {
            // Policy might not be settable
        }
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string folder = "", string? contentType = null)
    {
        var extension = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid():N}_{SanitizeFileName(Path.GetFileNameWithoutExtension(fileName))}{extension}";
        var objectName = string.IsNullOrEmpty(folder) ? uniqueName : $"{folder}/{uniqueName}";

        if (fileStream.CanSeek) fileStream.Position = 0;

        var putObjectArgs = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(fileStream)
            .WithObjectSize(fileStream.Length)
            .WithContentType(contentType ?? GetContentType(fileName));

        await _minioClient.PutObjectAsync(putObjectArgs);

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
            var objectName = ExtractObjectName(fileUrl);
            if (string.IsNullOrEmpty(objectName)) return false;

            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(removeObjectArgs);
            return true;
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
            var objectName = ExtractObjectName(fileUrl);
            if (string.IsNullOrEmpty(objectName)) return false;

            var statObjectArgs = new StatObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName);

            await _minioClient.StatObjectAsync(statObjectArgs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string GetPublicUrl(string fileKey, string folder)
    {
        var objectName = string.IsNullOrEmpty(folder) ? fileKey : $"{folder}/{fileKey}";
        var protocol = "http";
        return $"{protocol}://{_endpoint}/{_bucketName}/{objectName}";
    }

    /// <summary>Generate presigned URL untuk download temporer (berguna untuk MinIO tanpa public access)</summary>
    public async Task<string> GetPresignedUrlAsync(string fileUrl, int expirySeconds = 3600)
    {
        try
        {
            var objectName = ExtractObjectName(fileUrl);
            if (string.IsNullOrEmpty(objectName)) return fileUrl;

            var presignedGetArgs = new PresignedGetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(objectName)
                .WithExpiry(expirySeconds);

            return await _minioClient.PresignedGetObjectAsync(presignedGetArgs);
        }
        catch
        {
            return fileUrl;
        }
    }

    /// <summary>Ekstrak object name dari URL</summary>
    private string ExtractObjectName(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath.TrimStart('/');

            // URL format: /bucket/object_path
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
            _disposed = true;
        }
    }
}

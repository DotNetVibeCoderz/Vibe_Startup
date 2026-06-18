using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace LandLord.Services;

/// <summary>
/// Implementasi penyimpanan file menggunakan AWS S3 / MinIO (S3-compatible)
/// </summary>
public class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly long _maxFileSize;
    private readonly string[] _allowedExtensions;
    private readonly string _publicEndpoint;
    private readonly bool _isMinIO;

    public string ProviderName => _isMinIO ? "MinIO" : "AWS S3";

    public S3StorageService(IConfiguration configuration)
    {
        var provider = configuration.GetValue<string>("StorageProvider:Provider") ?? "S3";
        _isMinIO = provider.Equals("MinIO", StringComparison.OrdinalIgnoreCase);
        var configPrefix = _isMinIO ? "StorageProvider:MinIO" : "StorageProvider:S3";

        var accessKey = configuration.GetValue<string>($"{configPrefix}:AccessKey")
            ?? throw new InvalidOperationException($"{provider}: AccessKey is required.");
        var secretKey = configuration.GetValue<string>($"{configPrefix}:SecretKey")
            ?? throw new InvalidOperationException($"{provider}: SecretKey is required.");

        _bucketName = configuration.GetValue<string>($"{configPrefix}:BucketName") ?? "landlord-documents";
        var regionName = configuration.GetValue<string>($"{configPrefix}:Region") ?? "us-east-1";
        var serviceUrl = configuration.GetValue<string>($"{configPrefix}:ServiceUrl");
        _publicEndpoint = configuration.GetValue<string>($"{configPrefix}:PublicEndpoint") ?? "";

        _maxFileSize = configuration.GetValue<long>("StorageProvider:MaxFileSizeMB", 50) * 1024 * 1024;
        var extensions = configuration.GetSection("StorageProvider:AllowedExtensions").Get<string[]>();
        _allowedExtensions = extensions ?? new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".mp4", ".doc", ".docx", ".xls", ".xlsx" };

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var s3Config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(regionName),
            ForcePathStyle = _isMinIO, Timeout = TimeSpan.FromSeconds(30), MaxErrorRetry = 3
        };
        if (!string.IsNullOrEmpty(serviceUrl)) s3Config.ServiceURL = serviceUrl;
        _s3Client = new AmazonS3Client(credentials, s3Config);
        EnsureBucketExistsAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            var buckets = await _s3Client.ListBucketsAsync();
            if (!buckets.Buckets.Any(b => b.BucketName == _bucketName))
            {
                await _s3Client.PutBucketAsync(new PutBucketRequest { BucketName = _bucketName, UseClientRegion = true });
                if (!string.IsNullOrEmpty(_publicEndpoint) || _isMinIO) await SetPublicReadPolicyAsync();
            }
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        { throw new InvalidOperationException($"Bucket '{_bucketName}' already exists.", ex); }
    }

    private async Task SetPublicReadPolicyAsync()
    {
        var policy = $@"{{""Version"":""2012-10-17"",""Statement"":[{{""Sid"":""PublicRead"",""Effect"":""Allow"",""Principal"":""*"",""Action"":""s3:GetObject"",""Resource"":""arn:aws:s3:::{_bucketName}/*""}}]}}";
        try { await _s3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest { BucketName = _bucketName, Policy = policy }); } catch { }
    }

    public async Task<string> UploadAsync(string fileName, Stream fileStream, string contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(extension))
            throw new InvalidOperationException($"Ekstensi file '{extension}' tidak diizinkan.");
        if (fileStream.Length > _maxFileSize)
            throw new InvalidOperationException($"Ukuran file melebihi batas {_maxFileSize / 1024 / 1024} MB.");

        var uniqueKey = $"{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{extension}";
        var putRequest = new PutObjectRequest
        {
            BucketName = _bucketName, Key = uniqueKey, InputStream = fileStream,
            ContentType = contentType, AutoCloseStream = false, AutoResetStreamPosition = true,
            CannedACL = S3CannedACL.PublicRead
        };
        putRequest.Metadata.Add("original-filename", fileName);
        putRequest.Metadata.Add("uploaded-at", DateTime.UtcNow.ToString("O"));
        await _s3Client.PutObjectAsync(putRequest);
        return uniqueKey;
    }

    public async Task<Stream?> DownloadAsync(string filePath)
    {
        try { return (await _s3Client.GetObjectAsync(_bucketName, filePath)).ResponseStream; }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return null; }
    }

    public async Task<bool> DeleteAsync(string filePath)
    {
        try { await _s3Client.DeleteObjectAsync(_bucketName, filePath); return true; }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return false; }
    }

    public Task<string> GetPublicUrlAsync(string filePath)
    {
        var url = !string.IsNullOrEmpty(_publicEndpoint)
            ? $"{_publicEndpoint.TrimEnd('/')}/{_bucketName}/{filePath}"
            : _isMinIO ? $"{_s3Client.Config.ServiceURL}/{_bucketName}/{filePath}"
            : $"https://{_bucketName}.s3.{_s3Client.Config.RegionEndpoint?.SystemName}.amazonaws.com/{filePath}";
        return Task.FromResult(url);
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        try { await _s3Client.GetObjectMetadataAsync(_bucketName, filePath); return true; }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return false; }
    }

    public async Task<StorageFileInfo?> GetFileInfoAsync(string filePath)
    {
        try
        {
            var resp = await _s3Client.GetObjectMetadataAsync(_bucketName, filePath);
            return new StorageFileInfo
            {
                FileName = resp.Metadata.Keys.Contains("original-filename") ? resp.Metadata["original-filename"] : Path.GetFileName(filePath),
                FilePath = filePath,
                FileSize = resp.ContentLength,
                ContentType = resp.Headers.ContentType ?? "application/octet-stream",
                LastModified = resp.LastModified.GetValueOrDefault(),
                PublicUrl = await GetPublicUrlAsync(filePath)
            };
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return null; }
    }

    public async Task<List<StorageFileInfo>> ListFilesAsync(string? prefix = null, int maxResults = 100)
    {
        var files = new List<StorageFileInfo>();
        try
        {
            var req = new ListObjectsV2Request { BucketName = _bucketName, MaxKeys = maxResults };
            if (!string.IsNullOrEmpty(prefix)) req.Prefix = prefix;
            var resp = await _s3Client.ListObjectsV2Async(req);
            foreach (var obj in resp.S3Objects)
            {
                files.Add(new StorageFileInfo
                {
                    FileName = Path.GetFileName(obj.Key),
                    FilePath = obj.Key,
                    FileSize = obj.Size.GetValueOrDefault(),
                    ContentType = "application/octet-stream",
                    LastModified = obj.LastModified.GetValueOrDefault(),
                    PublicUrl = await GetPublicUrlAsync(obj.Key)
                });
            }
        }
        catch (AmazonS3Exception) { }
        return files;
    }

    public async Task<bool> CheckConnectionAsync()
    {
        try { var buckets = await _s3Client.ListBucketsAsync(); return buckets.Buckets.Any(b => b.BucketName == _bucketName); }
        catch { return false; }
    }

    /// <summary>Generate pre-signed URL untuk akses temporary</summary>
    public string GeneratePresignedUrl(string filePath, TimeSpan expiry)
    {
        return _s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucketName, Key = filePath,
            Expires = DateTime.UtcNow.Add(expiry), Verb = HttpVerb.GET
        });
    }
}

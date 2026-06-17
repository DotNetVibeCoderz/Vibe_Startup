using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Minio;
using Minio.DataModel.Args;

namespace Bioskop.Services;

/// <summary>
/// Unified storage service — FileSystem, Azure Blob, AWS S3, MinIO.
/// </summary>
public class StorageService
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<StorageService> _logger;

    private BlobServiceClient? _blobServiceClient;
    private BlobContainerClient? _blobContainerClient;
    private IAmazonS3? _s3Client;
    private IMinioClient? _minioClient;

    public StorageService(IConfiguration config, IWebHostEnvironment env, ILogger<StorageService> logger)
    {
        _config = config;
        _env = env;
        _logger = logger;
    }

    public string Provider => _config.GetValue<string>("Storage:Provider") ?? "FileSystem";
    public string BaseUrl => _config.GetValue<string>("Storage:BaseUrl") ?? "/uploads";

    /// <summary>Upload dari IFormFile (API controllers)</summary>
    public async Task<string?> UploadFileAsync(IFormFile file, string folder = "general")
    {
        if (file == null || file.Length == 0) return null;
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return await UploadBytesAsync(ms.ToArray(), file.FileName, file.ContentType, folder);
    }

    /// <summary>Upload dari byte array (Blazor InputFile, API)</summary>
    public async Task<string?> UploadBytesAsync(byte[] data, string fileName, string? contentType = null, string folder = "general")
    {
        if (data == null || data.Length == 0) return null;
        var ext = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid():N}{ext}";
        var relativePath = $"{folder}/{uniqueName}";
        return Provider switch
        {
            "AzureBlob" => await UploadToAzureBlobAsync(data, relativePath, contentType),
            "S3" => await UploadToS3Async(data, relativePath, contentType),
            "MinIO" => await UploadToMinIOAsync(data, relativePath, contentType),
            _ => await SaveToFileSystemAsync(data, relativePath)
        };
    }

    /// <summary>Download dari URL lalu simpan ke storage</summary>
    public async Task<string?> DownloadAndSaveAsync(string url, string folder = "downloads")
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var bytes = await client.GetByteArrayAsync(url);
            var ext = Path.GetExtension(url.Split('?')[0]);
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            return await UploadBytesAsync(bytes, $"download{ext}", null, folder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DownloadAndSaveAsync failed for {Url}", url);
            return null;
        }
    }

    /// <summary>Delete file</summary>
    public async Task<bool> DeleteFileAsync(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl)) return false;
        try
        {
            return Provider switch
            {
                "AzureBlob" => await DeleteFromAzureBlobAsync(fileUrl),
                "S3" => await DeleteFromS3Async(fileUrl),
                "MinIO" => await DeleteFromMinIOAsync(fileUrl),
                _ => DeleteFromFileSystem(fileUrl)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeleteFileAsync failed for {Url}", fileUrl);
            return false;
        }
    }

    public bool DeleteFile(string fileUrl) => DeleteFileAsync(fileUrl).GetAwaiter().GetResult();

    // ===== FILESYSTEM =====

    private async Task<string> SaveToFileSystemAsync(byte[] data, string relativePath)
    {
        var basePath = _config.GetValue<string>("Storage:BasePath") ?? "wwwroot/uploads";
        var fullPath = Path.Combine(_env.ContentRootPath, basePath, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        await File.WriteAllBytesAsync(fullPath, data);
        return $"{BaseUrl}/{relativePath.Replace('\\', '/')}";
    }

    private bool DeleteFromFileSystem(string fileUrl)
    {
        var basePath = _config.GetValue<string>("Storage:BasePath") ?? "wwwroot/uploads";
        if (!fileUrl.StartsWith(BaseUrl)) return false;
        var relativePath = fileUrl[BaseUrl.Length..].TrimStart('/');
        var fullPath = Path.Combine(_env.ContentRootPath, basePath, relativePath);
        if (File.Exists(fullPath)) { File.Delete(fullPath); return true; }
        return false;
    }

    // ===== AZURE BLOB =====

    private async Task<string> UploadToAzureBlobAsync(byte[] data, string relativePath, string? contentType)
    {
        var container = await GetAzureBlobContainerAsync();
        var blob = container.GetBlobClient(relativePath);
        var headers = new BlobHttpHeaders { ContentType = contentType ?? "application/octet-stream" };
        using var ms = new MemoryStream(data);
        await blob.UploadAsync(ms, new BlobUploadOptions { HttpHeaders = headers });
        var cdn = _config.GetValue<string>("Storage:AzureBlob:CdnUrl");
        return !string.IsNullOrEmpty(cdn) ? $"{cdn.TrimEnd('/')}/{relativePath}" : blob.Uri.ToString();
    }

    private async Task<bool> DeleteFromAzureBlobAsync(string fileUrl)
    {
        var container = await GetAzureBlobContainerAsync();
        var prefix = $"{container.Uri}/";
        var blobName = fileUrl.StartsWith(prefix) ? fileUrl[prefix.Length..] : new Uri(fileUrl).AbsolutePath.TrimStart('/');
        return await container.GetBlobClient(blobName).DeleteIfExistsAsync();
    }

    private async Task<BlobContainerClient> GetAzureBlobContainerAsync()
    {
        if (_blobContainerClient != null) return _blobContainerClient;
        var connStr = _config.GetValue<string>("Storage:AzureBlob:ConnectionString")
            ?? throw new InvalidOperationException("AzureBlob ConnectionString not configured");
        var name = _config.GetValue<string>("Storage:AzureBlob:ContainerName") ?? "bioskop";
        _blobServiceClient = new BlobServiceClient(connStr);
        _blobContainerClient = _blobServiceClient.GetBlobContainerClient(name);
        await _blobContainerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
        return _blobContainerClient;
    }

    // ===== AWS S3 =====

    private async Task<string> UploadToS3Async(byte[] data, string relativePath, string? contentType)
    {
        var client = GetS3Client();
        var bucket = _config.GetValue<string>("Storage:S3:BucketName")
            ?? throw new InvalidOperationException("S3 BucketName not configured");
        using var ms = new MemoryStream(data);
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket, Key = relativePath, InputStream = ms,
            ContentType = contentType ?? "application/octet-stream"
        });
        var cdn = _config.GetValue<string>("Storage:S3:CdnUrl");
        if (!string.IsNullOrEmpty(cdn)) return $"{cdn.TrimEnd('/')}/{relativePath}";
        var region = _config.GetValue<string>("Storage:S3:Region") ?? "us-east-1";
        return $"https://{bucket}.s3.{region}.amazonaws.com/{relativePath}";
    }

    private async Task<bool> DeleteFromS3Async(string fileUrl)
    {
        var client = GetS3Client();
        var bucket = _config.GetValue<string>("Storage:S3:BucketName")!;
        await client.DeleteObjectAsync(bucket, new Uri(fileUrl).AbsolutePath.TrimStart('/'));
        return true;
    }

    private IAmazonS3 GetS3Client()
    {
        if (_s3Client != null) return _s3Client;
        var key = _config.GetValue<string>("Storage:S3:AccessKey")
            ?? throw new InvalidOperationException("S3 AccessKey not configured");
        var secret = _config.GetValue<string>("Storage:S3:SecretKey")
            ?? throw new InvalidOperationException("S3 SecretKey not configured");
        var region = _config.GetValue<string>("Storage:S3:Region") ?? "us-east-1";
        var endpoint = _config.GetValue<string>("Storage:S3:Endpoint");
        var cfg = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
            ForcePathStyle = !string.IsNullOrEmpty(endpoint)
        };
        if (!string.IsNullOrEmpty(endpoint)) cfg.ServiceURL = endpoint;
        _s3Client = new AmazonS3Client(key, secret, cfg);
        return _s3Client;
    }

    // ===== MINIO =====

    private async Task<string> UploadToMinIOAsync(byte[] data, string relativePath, string? contentType)
    {
        var client = GetMinioClient();
        var bucket = _config.GetValue<string>("Storage:MinIO:BucketName")
            ?? throw new InvalidOperationException("MinIO BucketName not configured");
        if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket)))
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket));
        using var ms = new MemoryStream(data);
        await client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket).WithObject(relativePath).WithStreamData(ms)
            .WithObjectSize(data.Length).WithContentType(contentType ?? "application/octet-stream"));
        var cdn = _config.GetValue<string>("Storage:MinIO:CdnUrl");
        if (!string.IsNullOrEmpty(cdn)) return $"{cdn.TrimEnd('/')}/{relativePath}";
        var ep = _config.GetValue<string>("Storage:MinIO:Endpoint") ?? "localhost:9000";
        return $"{ep.TrimEnd('/')}/{bucket}/{relativePath}";
    }

    private async Task<bool> DeleteFromMinIOAsync(string fileUrl)
    {
        var client = GetMinioClient();
        var bucket = _config.GetValue<string>("Storage:MinIO:BucketName")!;
        var segments = new Uri(fileUrl).AbsolutePath.TrimStart('/').Split('/');
        var objName = string.Join('/', segments.Skip(1));
        await client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(objName));
        return true;
    }

    private IMinioClient GetMinioClient()
    {
        if (_minioClient != null) return _minioClient;
        var ep = _config.GetValue<string>("Storage:MinIO:Endpoint") ?? "localhost:9000";
        var key = _config.GetValue<string>("Storage:MinIO:AccessKey")
            ?? throw new InvalidOperationException("MinIO AccessKey not configured");
        var secret = _config.GetValue<string>("Storage:MinIO:SecretKey")
            ?? throw new InvalidOperationException("MinIO SecretKey not configured");
        _minioClient = new MinioClient().WithEndpoint(ep).WithCredentials(key, secret).Build();
        return _minioClient;
    }
}

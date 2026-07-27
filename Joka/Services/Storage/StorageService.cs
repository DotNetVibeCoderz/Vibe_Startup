// Storage abstraction with four providers, selected by Storage:Provider.
//
// Everything writes through IStorageService, so switching provider is a config
// change - no call site knows which backend it is talking to.
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Minio;
using Minio.DataModel.Args;

namespace Joka.Services.Storage;

public interface IStorageService
{
    Task<string> UploadAsync(string fileName, Stream content, string contentType);
    Task<Stream?> DownloadAsync(string filePath);
    Task<bool> DeleteAsync(string filePath);
    string GetPublicUrl(string filePath);
}

// ---------------------------------------------------------------------------
// FileSystem - the default, and the only one that needs no credentials
// ---------------------------------------------------------------------------
public class FileSystemStorageService : IStorageService
{
    private readonly string _basePath;
    private readonly IWebHostEnvironment _env;

    public FileSystemStorageService(IConfiguration config, IWebHostEnvironment env)
    {
        _env = env;
        _basePath = config["Storage:FileSystem:BasePath"] ?? "wwwroot/uploads";
    }

    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        var filePath = Path.Combine(_env.ContentRootPath, _basePath, fileName);
        var dir = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await using var fileStream = File.Create(filePath);
        await content.CopyToAsync(fileStream);

        // wwwroot is the web root, so strip it from the public URL.
        var publicPath = _basePath.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase)
            ? _basePath["wwwroot/".Length..]
            : _basePath;

        return "/" + Path.Combine(publicPath, fileName).Replace("\\", "/");
    }

    public Task<Stream?> DownloadAsync(string filePath)
    {
        var fullPath = ResolveLocalPath(filePath);
        return Task.FromResult<Stream?>(File.Exists(fullPath) ? File.OpenRead(fullPath) : null);
    }

    public Task<bool> DeleteAsync(string filePath)
    {
        var fullPath = ResolveLocalPath(filePath);
        if (!File.Exists(fullPath)) return Task.FromResult(false);

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    public string GetPublicUrl(string filePath) => filePath;

    private string ResolveLocalPath(string filePath)
    {
        var relative = filePath.TrimStart('/');

        // A returned URL has wwwroot stripped; put it back to hit the file.
        if (_basePath.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase)
            && !relative.StartsWith("wwwroot/", StringComparison.OrdinalIgnoreCase))
            relative = "wwwroot/" + relative;

        return Path.Combine(_env.ContentRootPath, relative);
    }
}

// ---------------------------------------------------------------------------
// Azure Blob Storage
// ---------------------------------------------------------------------------
public class AzureBlobStorageService : IStorageService
{
    private readonly BlobContainerClient _container;

    public AzureBlobStorageService(IConfiguration config)
    {
        var connectionString = config["Storage:AzureBlob:ConnectionString"];
        var containerName = config["Storage:AzureBlob:ContainerName"] ?? "joka-storage";

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Storage:AzureBlob:ConnectionString belum diisi. Isi dulu atau ganti Storage:Provider ke FileSystem.");

        _container = new BlobContainerClient(connectionString, containerName);
    }

    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        await _container.CreateIfNotExistsAsync();

        var blob = _container.GetBlobClient(fileName);
        await blob.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType });

        return blob.Uri.ToString();
    }

    public async Task<Stream?> DownloadAsync(string filePath)
    {
        var blob = _container.GetBlobClient(BlobNameFrom(filePath));
        if (!await blob.ExistsAsync()) return null;

        var buffer = new MemoryStream();
        await blob.DownloadToAsync(buffer);
        buffer.Position = 0;
        return buffer;
    }

    public async Task<bool> DeleteAsync(string filePath) =>
        await _container.GetBlobClient(BlobNameFrom(filePath)).DeleteIfExistsAsync();

    public string GetPublicUrl(string filePath) =>
        filePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? filePath
            : _container.GetBlobClient(filePath).Uri.ToString();

    /// <summary>Upload returns a full URL, so accept either that or a bare name.</summary>
    private string BlobNameFrom(string filePath) =>
        Uri.TryCreate(filePath, UriKind.Absolute, out var uri)
            ? string.Join('/', uri.Segments.Skip(2)).TrimStart('/')
            : filePath.TrimStart('/');
}

// ---------------------------------------------------------------------------
// AWS S3 (also works against any S3-compatible endpoint via ServiceUrl)
// ---------------------------------------------------------------------------
public class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private readonly string _publicBase;

    public S3StorageService(IConfiguration config)
    {
        var accessKey = config["Storage:S3:AccessKey"];
        var secretKey = config["Storage:S3:SecretKey"];
        var region = config["Storage:S3:Region"] ?? "ap-southeast-1";
        var serviceUrl = config["Storage:S3:ServiceUrl"];
        _bucket = config["Storage:S3:BucketName"] ?? "joka-storage";

        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException(
                "Storage:S3 AccessKey/SecretKey belum diisi. Isi dulu atau ganti Storage:Provider ke FileSystem.");

        var s3Config = new AmazonS3Config();

        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            // Non-AWS S3 endpoint: path style is required by most of them.
            s3Config.ServiceURL = serviceUrl;
            s3Config.ForcePathStyle = true;
            _publicBase = $"{serviceUrl.TrimEnd('/')}/{_bucket}";
        }
        else
        {
            s3Config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
            _publicBase = $"https://{_bucket}.s3.{region}.amazonaws.com";
        }

        _client = new AmazonS3Client(accessKey, secretKey, s3Config);
    }

    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = fileName,
            InputStream = content,
            ContentType = contentType
        });

        return GetPublicUrl(fileName);
    }

    public async Task<Stream?> DownloadAsync(string filePath)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucket, KeyFrom(filePath));

            var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer);
            buffer.Position = 0;
            return buffer;
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
            await _client.DeleteObjectAsync(_bucket, KeyFrom(filePath));
            return true;
        }
        catch (AmazonS3Exception)
        {
            return false;
        }
    }

    public string GetPublicUrl(string filePath) =>
        filePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? filePath
            : $"{_publicBase}/{filePath.TrimStart('/')}";

    private static string KeyFrom(string filePath) =>
        Uri.TryCreate(filePath, UriKind.Absolute, out var uri)
            ? uri.AbsolutePath.TrimStart('/')
            : filePath.TrimStart('/');
}

// ---------------------------------------------------------------------------
// MinIO
// ---------------------------------------------------------------------------
public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _client;
    private readonly string _bucket;
    private readonly string _publicBase;

    public MinioStorageService(IConfiguration config)
    {
        var endpoint = config["Storage:MinIO:Endpoint"] ?? "localhost:9000";
        var accessKey = config["Storage:MinIO:AccessKey"];
        var secretKey = config["Storage:MinIO:SecretKey"];
        var useSsl = config.GetValue("Storage:MinIO:UseSSL", false);
        _bucket = config["Storage:MinIO:BucketName"] ?? "joka-storage";

        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException(
                "Storage:MinIO AccessKey/SecretKey belum diisi. Isi dulu atau ganti Storage:Provider ke FileSystem.");

        _client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();

        _publicBase = $"{(useSsl ? "https" : "http")}://{endpoint}/{_bucket}";
    }

    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucket));
        if (!exists)
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket));

        // MinIO needs the length up front; a browser upload stream may not have it.
        Stream payload = content.CanSeek ? content : await BufferAsync(content);
        payload.Position = 0;

        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(fileName)
            .WithStreamData(payload)
            .WithObjectSize(payload.Length)
            .WithContentType(contentType));

        return $"{_publicBase}/{fileName}";
    }

    public async Task<Stream?> DownloadAsync(string filePath)
    {
        try
        {
            var buffer = new MemoryStream();

            await _client.GetObjectAsync(new GetObjectArgs()
                .WithBucket(_bucket)
                .WithObject(KeyFrom(filePath))
                .WithCallbackStream(stream => stream.CopyTo(buffer)));

            buffer.Position = 0;
            return buffer;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(string filePath)
    {
        try
        {
            await _client.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(_bucket)
                .WithObject(KeyFrom(filePath)));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public string GetPublicUrl(string filePath) =>
        filePath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? filePath
            : $"{_publicBase}/{filePath.TrimStart('/')}";

    private static async Task<MemoryStream> BufferAsync(Stream source)
    {
        var buffer = new MemoryStream();
        await source.CopyToAsync(buffer);
        return buffer;
    }

    private string KeyFrom(string filePath) =>
        filePath.StartsWith(_publicBase, StringComparison.OrdinalIgnoreCase)
            ? filePath[(_publicBase.Length + 1)..]
            : filePath.TrimStart('/');
}

// ---------------------------------------------------------------------------
// Factory
// ---------------------------------------------------------------------------
public class StorageServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;
    private readonly ILogger<StorageServiceFactory> _logger;

    public StorageServiceFactory(
        IServiceProvider serviceProvider, IConfiguration config, ILogger<StorageServiceFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Falls back to FileSystem when the configured provider cannot start -
    /// a missing credential should degrade uploads, not take the app down.
    /// </summary>
    public IStorageService Create()
    {
        var provider = _config["Storage:Provider"] ?? "FileSystem";

        try
        {
            return provider switch
            {
                "AzureBlob" => new AzureBlobStorageService(_config),
                "S3" => new S3StorageService(_config),
                "MinIO" => new MinioStorageService(_config),
                _ => _serviceProvider.GetRequiredService<FileSystemStorageService>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage provider {Provider} gagal dibuat, kembali ke FileSystem", provider);
            return _serviceProvider.GetRequiredService<FileSystemStorageService>();
        }
    }

    /// <summary>Which provider is actually in use, for the health page.</summary>
    public string ActiveProvider
    {
        get
        {
            var configured = _config["Storage:Provider"] ?? "FileSystem";
            return Create() is FileSystemStorageService && configured != "FileSystem"
                ? $"FileSystem (fallback dari {configured})"
                : configured;
        }
    }
}

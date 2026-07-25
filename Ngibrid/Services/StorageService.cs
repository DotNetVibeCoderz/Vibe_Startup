using Minio;
using Minio.DataModel.Args;

namespace Ngibrid.Services;

/// <summary>
/// Generic storage provider supporting FileSystem, AzureBlob, S3, and MinIO.
/// Upload/Delete/Exists are implemented for every provider so switching Storage:Provider
/// in appsettings.json needs no code change.
/// </summary>
public class StorageService
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<StorageService> _logger;
    private readonly string _provider;
    private readonly string _basePath;
    private readonly long _maxFileSizeBytes;

    public StorageService(IConfiguration config, IWebHostEnvironment env, ILogger<StorageService> logger)
    {
        _config = config;
        _env = env;
        _logger = logger;
        _provider = config["Storage:Provider"] ?? "FileSystem";
        _basePath = (config["Storage:BasePath"] ?? "wwwroot/uploads").Replace("wwwroot/", "").Trim('/');
        _maxFileSizeBytes = config.GetValue<long>("Storage:MaxFileSizeMb", 25) * 1024 * 1024;
    }

    public string Provider => _provider;

    /// <summary>Extensions accepted for upload, from Storage:AllowedExtensions.</summary>
    public string[] AllowedExtensions =>
        _config.GetSection("Storage:AllowedExtensions").Get<string[]>()
        ?? new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt" };

    public long MaxFileSizeBytes => _maxFileSizeBytes;

    /// <summary>
    /// Validate name/extension before any bytes are read, so an oversized or disallowed
    /// upload is rejected without touching storage.
    /// </summary>
    public (bool Ok, string? Error) Validate(string fileName, long sizeBytes)
    {
        if (sizeBytes > _maxFileSizeBytes)
            return (false, $"Ukuran file melebihi batas {_maxFileSizeBytes / 1024 / 1024} MB.");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(ext))
            return (false, "File harus memiliki ekstensi.");

        if (!AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            return (false, $"Tipe file {ext} tidak diizinkan. Diizinkan: {string.Join(", ", AllowedExtensions)}");

        return (true, null);
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string? folder = null,
        CancellationToken cancellationToken = default)
    {
        // CanSeek is false for browser upload streams, so size is enforced by the caller via Validate().
        if (fileStream.CanSeek && fileStream.Length > _maxFileSizeBytes)
            throw new InvalidOperationException($"File size exceeds max of {_maxFileSizeBytes / 1024 / 1024}MB");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var uniqueName = $"{Guid.NewGuid():N}{ext}";
        var relativePath = string.IsNullOrEmpty(folder)
            ? uniqueName
            : $"{folder.Trim('/')}/{uniqueName}";

        return _provider.ToLowerInvariant() switch
        {
            "azureblob" => await UploadToAzureBlobAsync(fileStream, relativePath, cancellationToken),
            "s3" => await UploadToS3Async(fileStream, relativePath, cancellationToken),
            "minio" => await UploadToMinIOAsync(fileStream, relativePath, cancellationToken),
            _ => await UploadToFileSystemAsync(fileStream, relativePath, cancellationToken)
        };
    }

    private async Task<string> UploadToFileSystemAsync(Stream fileStream, string relativePath, CancellationToken ct)
    {
        var root = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var fullPath = Path.Combine(root, _basePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await fileStream.CopyToAsync(fs, ct);
        return $"/{_basePath}/{relativePath}".Replace("\\", "/");
    }

    private async Task<string> UploadToAzureBlobAsync(Stream fileStream, string relativePath, CancellationToken ct)
    {
        var connStr = _config["Storage:AzureBlob:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connStr))
            throw new InvalidOperationException("Storage:AzureBlob:ConnectionString belum dikonfigurasi.");

        var container = _config["Storage:AzureBlob:ContainerName"] ?? "ngibrid-storage";
        var client = new Azure.Storage.Blobs.BlobContainerClient(connStr, container);
        await client.CreateIfNotExistsAsync(cancellationToken: ct);
        var blob = client.GetBlobClient(relativePath);
        await blob.UploadAsync(fileStream, overwrite: true, cancellationToken: ct);
        return blob.Uri.ToString();
    }

    private async Task<string> UploadToS3Async(Stream fileStream, string relativePath, CancellationToken ct)
    {
        var accessKey = _config["Storage:S3:AccessKey"];
        var secretKey = _config["Storage:S3:SecretKey"];
        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("Storage:S3 credentials belum dikonfigurasi.");

        var serviceUrl = _config["Storage:S3:ServiceUrl"];
        var s3Config = new Amazon.S3.AmazonS3Config { ForcePathStyle = true };
        if (!string.IsNullOrWhiteSpace(serviceUrl)) s3Config.ServiceURL = serviceUrl;
        else s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_config["Storage:S3:Region"] ?? "us-east-1");

        using var client = new Amazon.S3.AmazonS3Client(accessKey, secretKey, s3Config);
        var bucket = _config["Storage:S3:BucketName"] ?? "ngibrid-storage";

        await client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
        {
            BucketName = bucket,
            Key = relativePath,
            InputStream = fileStream,
            ContentType = MimeTypes.GetMimeType(relativePath)
        }, ct);

        return string.IsNullOrWhiteSpace(serviceUrl)
            ? $"https://{bucket}.s3.amazonaws.com/{relativePath}"
            : $"{serviceUrl.TrimEnd('/')}/{bucket}/{relativePath}";
    }

    private async Task<string> UploadToMinIOAsync(Stream fileStream, string relativePath, CancellationToken ct)
    {
        var (client, bucket, endpoint, useSsl) = BuildMinioClient();

        var exists = await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
        if (!exists)
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);

        // MinIO needs the object length up front; buffer non-seekable streams so uploads still work.
        Stream upload = fileStream;
        MemoryStream? buffer = null;
        if (!fileStream.CanSeek)
        {
            buffer = new MemoryStream();
            await fileStream.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            upload = buffer;
        }

        try
        {
            await client.PutObjectAsync(new PutObjectArgs()
                .WithBucket(bucket)
                .WithObject(relativePath)
                .WithStreamData(upload)
                .WithObjectSize(upload.Length)
                .WithContentType(MimeTypes.GetMimeType(relativePath)), ct);
        }
        finally
        {
            if (buffer != null) await buffer.DisposeAsync();
        }

        var scheme = useSsl ? "https" : "http";
        return $"{scheme}://{endpoint}/{bucket}/{relativePath}";
    }

    private (IMinioClient Client, string Bucket, string Endpoint, bool UseSsl) BuildMinioClient()
    {
        var endpoint = _config["Storage:MinIO:Endpoint"] ?? "localhost:9000";
        var accessKey = _config["Storage:MinIO:AccessKey"] ?? "minioadmin";
        var secretKey = _config["Storage:MinIO:SecretKey"] ?? "minioadmin";
        var bucket = _config["Storage:MinIO:BucketName"] ?? "ngibrid-storage";
        var useSsl = _config.GetValue("Storage:MinIO:UseSSL", false);

        var client = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();

        return (client, bucket, endpoint, useSsl);
    }

    public async Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return;

        try
        {
            switch (_provider.ToLowerInvariant())
            {
                case "azureblob":
                {
                    var connStr = _config["Storage:AzureBlob:ConnectionString"];
                    var container = _config["Storage:AzureBlob:ContainerName"] ?? "ngibrid-storage";
                    var client = new Azure.Storage.Blobs.BlobContainerClient(connStr, container);
                    await client.GetBlobClient(ExtractObjectKey(fileUrl))
                        .DeleteIfExistsAsync(cancellationToken: cancellationToken);
                    break;
                }
                case "s3":
                {
                    var serviceUrl = _config["Storage:S3:ServiceUrl"];
                    var s3Config = new Amazon.S3.AmazonS3Config { ForcePathStyle = true };
                    if (!string.IsNullOrWhiteSpace(serviceUrl)) s3Config.ServiceURL = serviceUrl;
                    else s3Config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_config["Storage:S3:Region"] ?? "us-east-1");

                    using var client = new Amazon.S3.AmazonS3Client(
                        _config["Storage:S3:AccessKey"], _config["Storage:S3:SecretKey"], s3Config);
                    await client.DeleteObjectAsync(_config["Storage:S3:BucketName"] ?? "ngibrid-storage",
                        ExtractObjectKey(fileUrl), cancellationToken);
                    break;
                }
                case "minio":
                {
                    var (client, bucket, _, _) = BuildMinioClient();
                    await client.RemoveObjectAsync(new RemoveObjectArgs()
                        .WithBucket(bucket)
                        .WithObject(ExtractObjectKey(fileUrl)), cancellationToken);
                    break;
                }
                default:
                {
                    var root = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                    var relative = fileUrl.TrimStart('/');
                    var fullPath = Path.GetFullPath(
                        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

                    // Confine deletion to the upload directory. Every URL we store is generated
                    // server-side, but a caller-supplied "/../../appsettings.json" must not be able
                    // to turn this into arbitrary file deletion if that ever stops being true.
                    var uploadRoot = Path.GetFullPath(Path.Combine(root, _basePath));
                    if (!fullPath.StartsWith(uploadRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("Refused to delete {Url}: outside the upload directory.", fileUrl);
                        break;
                    }

                    if (File.Exists(fullPath)) File.Delete(fullPath);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            // Deleting an attachment must never take down the caller's operation.
            _logger.LogWarning(ex, "Failed to delete {Url} from {Provider}", fileUrl, _provider);
        }
    }

    /// <summary>Strip scheme/host/bucket to recover the storage object key from a public URL.</summary>
    private string ExtractObjectKey(string fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
            return fileUrl.TrimStart('/');

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        var bucket = _provider.Equals("minio", StringComparison.OrdinalIgnoreCase)
            ? _config["Storage:MinIO:BucketName"]
            : _config["Storage:S3:BucketName"];

        if (segments.Length > 1 && segments[0].Equals(bucket, StringComparison.OrdinalIgnoreCase))
            return string.Join('/', segments.Skip(1));

        return string.Join('/', segments);
    }
}

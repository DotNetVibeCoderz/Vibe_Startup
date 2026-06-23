using Azure.Storage.Blobs;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Minio;
using Minio.DataModel.Args;

namespace EventSphere.Services;

/// <summary>
/// Storage abstraction: FileSystem, AzureBlob, AWS S3, MinIO
/// </summary>
public class StorageService
{
    private readonly IConfiguration _config;
    private readonly ILogger<StorageService> _logger;
    private readonly string _provider;
    private readonly string _basePath;

    public StorageService(IConfiguration config, ILogger<StorageService> logger)
    {
        _config = config;
        _logger = logger;
        _provider = (config.GetValue<string>("Storage:Provider") ?? "FileSystem").Trim();
        _basePath = config.GetValue<string>("Storage:FileSystem:BasePath") ?? "wwwroot/uploads";

        _logger.LogInformation("Storage provider: {Provider}, BasePath: {BasePath}", _provider, _basePath);
    }

    // ═══ PUBLIC API ═══

    /// <summary>
    /// Upload stream ke storage dan return URL.
    /// </summary>
    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, string? folder = null)
    {
        _logger.LogDebug("UploadFile: provider={Provider}, file={FileName}, folder={Folder}", _provider, fileName, folder);

        return _provider.ToLowerInvariant() switch
        {
            "azureblob" => await UploadToAzureBlobAsync(stream, fileName, contentType, folder),
            "s3" => await UploadToS3Async(stream, fileName, contentType, folder),
            "minio" => await UploadToMinIOAsync(stream, fileName, contentType, folder),
            _ => await UploadToFileSystemAsync(stream, fileName, folder)
        };
    }

    public async Task<string> UploadAsync(IFormFile file, string? folder = null)
    {
        using var stream = file.OpenReadStream();
        return await UploadAsync(stream, file.FileName, file.ContentType, folder);
    }

    public async Task<bool> DeleteAsync(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl)) return false;
        return _provider.ToLowerInvariant() switch
        {
            "azureblob" => await DeleteFromAzureBlobAsync(fileUrl),
            "s3" => await DeleteFromS3Async(fileUrl),
            "minio" => await DeleteFromMinIOAsync(fileUrl),
            _ => DeleteFromFileSystem(fileUrl)
        };
    }

    // ═══ FILE SYSTEM ═══

    private async Task<string> UploadToFileSystemAsync(Stream stream, string fileName, string? folder)
    {
        var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), _basePath, folder ?? "");
        Directory.CreateDirectory(uploadDir);

        var uniqueName = $"{Guid.NewGuid()}_{SanitizeFileName(fileName)}";
        var filePath = Path.Combine(uploadDir, uniqueName);

        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fs);

        var url = $"/uploads/{folder}/{uniqueName}".Replace("//", "/");
        _logger.LogInformation("File uploaded to FileSystem: {Url}", url);
        return url;
    }

    private bool DeleteFromFileSystem(string fileUrl)
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", fileUrl.TrimStart('/'));
        if (File.Exists(filePath)) { File.Delete(filePath); return true; }
        return false;
    }

    // ═══ AZURE BLOB ═══

    private BlobServiceClient GetAzureBlob()
    {
        var cs = _config.GetValue<string>("Storage:AzureBlob:ConnectionString") ?? "";
        if (string.IsNullOrEmpty(cs)) throw new InvalidOperationException("AzureBlob ConnectionString not set.");
        return new BlobServiceClient(cs);
    }

    private async Task<string> UploadToAzureBlobAsync(Stream stream, string fileName, string contentType, string? folder)
    {
        var client = GetAzureBlob();
        var containerName = _config.GetValue<string>("Storage:AzureBlob:ContainerName") ?? "eventsphere";
        var container = client.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync();

        var blobPath = $"{folder}/{Guid.NewGuid()}_{SanitizeFileName(fileName)}".TrimStart('/');
        var blob = container.GetBlobClient(blobPath);
        await blob.UploadAsync(stream, new Azure.Storage.Blobs.Models.BlobUploadOptions
        {
            HttpHeaders = new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType }
        });

        _logger.LogInformation("File uploaded to AzureBlob: {Path}", blob.Uri);
        return blob.Uri.ToString();
    }

    private async Task<bool> DeleteFromAzureBlobAsync(string fileUrl)
    {
        try
        {
            var client = GetAzureBlob();
            var containerName = _config.GetValue<string>("Storage:AzureBlob:ContainerName") ?? "eventsphere";
            var container = client.GetBlobContainerClient(containerName);
            var idx = fileUrl.IndexOf(containerName, StringComparison.OrdinalIgnoreCase);
            var blobName = idx >= 0 ? fileUrl[(idx + containerName.Length + 1)..].Split('?')[0] : new Uri(fileUrl).AbsolutePath.TrimStart('/');
            await container.GetBlobClient(blobName).DeleteIfExistsAsync();
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Delete AzureBlob failed: {Url}", fileUrl); return false; }
    }

    // ═══ AWS S3 ═══

    private AmazonS3Client GetS3()
    {
        var key = _config.GetValue<string>("Storage:S3:AccessKey") ?? "";
        var secret = _config.GetValue<string>("Storage:S3:SecretKey") ?? "";
        var region = _config.GetValue<string>("Storage:S3:Region") ?? "us-east-1";
        var svcUrl = _config.GetValue<string>("Storage:S3:ServiceUrl") ?? "";

        if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("S3 AccessKey not set.");

        var s3Config = new AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region) };
        if (!string.IsNullOrEmpty(svcUrl)) { s3Config.ServiceURL = svcUrl; s3Config.ForcePathStyle = true; }
        return new AmazonS3Client(new BasicAWSCredentials(key, secret), s3Config);
    }

    private async Task<string> UploadToS3Async(Stream stream, string fileName, string contentType, string? folder)
    {
        var client = GetS3();
        var bucket = _config.GetValue<string>("Storage:S3:BucketName") ?? "eventsphere";
        var key = $"{folder}/{Guid.NewGuid()}_{SanitizeFileName(fileName)}".TrimStart('/');

        if (!await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(client, bucket))
            await client.PutBucketAsync(bucket);

        await client.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = key, InputStream = stream, ContentType = contentType });
        _logger.LogInformation("File uploaded to S3: {Bucket}/{Key}", bucket, key);

        var svcUrl = _config.GetValue<string>("Storage:S3:ServiceUrl") ?? "";
        return !string.IsNullOrEmpty(svcUrl) ? $"{svcUrl}/{bucket}/{key}" : $"https://{bucket}.s3.amazonaws.com/{key}";
    }

    private async Task<bool> DeleteFromS3Async(string fileUrl)
    {
        try
        {
            var client = GetS3();
            var bucket = _config.GetValue<string>("Storage:S3:BucketName") ?? "eventsphere";
            var key = ExtractKey(fileUrl, bucket);
            await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = key });
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Delete S3 failed: {Url}", fileUrl); return false; }
    }

    // ═══ MINIO ═══

    private IMinioClient GetMinio()
    {
        var endpoint = _config.GetValue<string>("Storage:MinIO:Endpoint") ?? "localhost:9000";
        var key = _config.GetValue<string>("Storage:MinIO:AccessKey") ?? "";
        var secret = _config.GetValue<string>("Storage:MinIO:SecretKey") ?? "";
        if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("MinIO AccessKey not set.");
        return new MinioClient().WithEndpoint(endpoint).WithCredentials(key, secret).Build();
    }

    private async Task<string> UploadToMinIOAsync(Stream stream, string fileName, string contentType, string? folder)
    {
        var client = GetMinio();
        var bucket = _config.GetValue<string>("Storage:MinIO:BucketName") ?? "eventsphere";

        if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket)))
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket));

        var objName = $"{folder}/{Guid.NewGuid()}_{SanitizeFileName(fileName)}".TrimStart('/');
        await client.PutObjectAsync(new PutObjectArgs().WithBucket(bucket).WithObject(objName).WithStreamData(stream).WithObjectSize(stream.Length).WithContentType(contentType));

        var ep = _config.GetValue<string>("Storage:MinIO:Endpoint") ?? "localhost:9000";
        _logger.LogInformation("File uploaded to MinIO: {Bucket}/{Object}", bucket, objName);
        return $"http://{ep}/{bucket}/{objName}";
    }

    private async Task<bool> DeleteFromMinIOAsync(string fileUrl)
    {
        try
        {
            var client = GetMinio();
            var bucket = _config.GetValue<string>("Storage:MinIO:BucketName") ?? "eventsphere";
            var obj = ExtractKey(fileUrl, bucket);
            await client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(obj));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Delete MinIO failed: {Url}", fileUrl); return false; }
    }

    // ═══ HELPERS ═══

    private static string SanitizeFileName(string name)
    {
        var inv = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(inv, StringSplitOptions.RemoveEmptyEntries)).Replace(" ", "_");
    }

    private static string ExtractKey(string url, string bucket)
    {
        var idx = url.IndexOf(bucket, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0) { var start = idx + bucket.Length + 1; return start < url.Length ? url[start..].Split('?')[0] : ""; }
        return new Uri(url).AbsolutePath.TrimStart('/');
    }
}

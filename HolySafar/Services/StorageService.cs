using Azure.Storage.Blobs;
using Amazon.S3;
using Amazon.S3.Transfer;
using Minio;
using Minio.DataModel.Args;

namespace HolySafar.Services;

/// <summary>
/// Multi-provider storage service: FileSystem | AzureBlob | S3 | MinIO
/// </summary>
public class StorageService
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public StorageService(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    public string Provider => _config["Storage:Provider"] ?? "FileSystem";

    /// <summary>
    /// Upload file and return public URL
    /// </summary>
    public async Task<string> UploadAsync(Stream stream, string fileName, string folder = "general")
    {
        return Provider switch
        {
            "AzureBlob" => await UploadToAzureBlobAsync(stream, fileName, folder),
            "S3" => await UploadToS3Async(stream, fileName, folder),
            "MinIO" => await UploadToMinIOAsync(stream, fileName, folder),
            _ => await UploadToFileSystemAsync(stream, fileName, folder)
        };
    }

    /// <summary>
    /// Delete a file by URL
    /// </summary>
    public async Task<bool> DeleteAsync(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl)) return false;

        return Provider switch
        {
            "AzureBlob" => await DeleteFromAzureBlobAsync(fileUrl),
            "S3" => await DeleteFromS3Async(fileUrl),
            "MinIO" => await DeleteFromMinIOAsync(fileUrl),
            _ => DeleteFromFileSystem(fileUrl)
        };
    }

    // ==================== FILESYSTEM ====================

    private async Task<string> UploadToFileSystemAsync(Stream stream, string fileName, string folder)
    {
        var basePath = _config["Storage:FileSystem:BasePath"] ?? "wwwroot/uploads";
        var uploadDir = Path.Combine(_env.WebRootPath ?? "wwwroot", "uploads", folder);

        if (!Directory.Exists(uploadDir))
            Directory.CreateDirectory(uploadDir);

        var ext = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadDir, uniqueName);

        await using var fileStream = File.Create(filePath);
        await stream.CopyToAsync(fileStream);

        return $"/uploads/{folder}/{uniqueName}";
    }

    private bool DeleteFromFileSystem(string fileUrl)
    {
        var filePath = Path.Combine(_env.WebRootPath ?? "wwwroot", fileUrl.TrimStart('/'));
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return true;
        }
        return false;
    }

    // ==================== AZURE BLOB ====================

    private async Task<string> UploadToAzureBlobAsync(Stream stream, string fileName, string folder)
    {
        try
        {

        var connStr = _config["Storage:AzureBlob:ConnectionString"];
        var container = _config["Storage:AzureBlob:ContainerName"] ?? "holysafar";

        if (string.IsNullOrEmpty(connStr))
            throw new InvalidOperationException("AzureBlob ConnectionString is not configured.");

        var blobServiceClient = new BlobServiceClient(connStr);
        var containerClient = blobServiceClient.GetBlobContainerClient(container);
        await containerClient.CreateIfNotExistsAsync();

        var ext = Path.GetExtension(fileName);
        var blobName = $"{folder}/{Guid.NewGuid():N}{ext}";
        var blobClient = containerClient.GetBlobClient(blobName);

        //stream.Position = 0;
        await blobClient.UploadAsync(stream, overwrite: true);

        return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return default;
        }

    }

    private async Task<bool> DeleteFromAzureBlobAsync(string fileUrl)
    {
        try
        {
            var connStr = _config["Storage:AzureBlob:ConnectionString"];
            var container = _config["Storage:AzureBlob:ContainerName"] ?? "holysafar";

            if (string.IsNullOrEmpty(connStr)) return false;

            var blobServiceClient = new BlobServiceClient(connStr);
            var uri = new Uri(fileUrl);
            var blobName = string.Join("", uri.Segments.Skip(2)); // skip /container/
            var containerClient = blobServiceClient.GetBlobContainerClient(container);
            var blobClient = containerClient.GetBlobClient(blobName);
            return await blobClient.DeleteIfExistsAsync();
        }
        catch { return false; }
    }

    // ==================== AWS S3 ====================

    private async Task<string> UploadToS3Async(Stream stream, string fileName, string folder)
    {
        var accessKey = _config["Storage:S3:AccessKey"];
        var secretKey = _config["Storage:S3:SecretKey"];
        var bucketName = _config["Storage:S3:BucketName"] ?? "holysafar";
        var region = _config["Storage:S3:Region"] ?? "us-east-1";

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
            throw new InvalidOperationException("S3 AccessKey/SecretKey is not configured.");

        using var s3Client = new AmazonS3Client(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region));
        using var transferUtility = new TransferUtility(s3Client);

        var ext = Path.GetExtension(fileName);
        var key = $"{folder}/{Guid.NewGuid():N}{ext}";

        stream.Position = 0;
        await transferUtility.UploadAsync(stream, bucketName, key);

        return $"https://{bucketName}.s3.{region}.amazonaws.com/{key}";
    }

    private async Task<bool> DeleteFromS3Async(string fileUrl)
    {
        try
        {
            var accessKey = _config["Storage:S3:AccessKey"];
            var secretKey = _config["Storage:S3:SecretKey"];
            var bucketName = _config["Storage:S3:BucketName"] ?? "holysafar";
            var region = _config["Storage:S3:Region"] ?? "us-east-1";

            if (string.IsNullOrEmpty(accessKey)) return false;

            using var s3Client = new AmazonS3Client(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region));
            var uri = new Uri(fileUrl);
            var key = uri.AbsolutePath.TrimStart('/');
            await s3Client.DeleteObjectAsync(bucketName, key);
            return true;
        }
        catch { return false; }
    }

    // ==================== MINIO ====================

    private async Task<string> UploadToMinIOAsync(Stream stream, string fileName, string folder)
    {
        var endpoint = _config["Storage:MinIO:Endpoint"];
        var accessKey = _config["Storage:MinIO:AccessKey"];
        var secretKey = _config["Storage:MinIO:SecretKey"];
        var bucketName = _config["Storage:MinIO:BucketName"] ?? "holysafar";

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(accessKey))
            throw new InvalidOperationException("MinIO Endpoint/AccessKey is not configured.");

        using var minioClient = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .Build();

        // Ensure bucket exists
        bool found = await minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName));
        if (!found)
            await minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName));

        var ext = Path.GetExtension(fileName);
        var objectName = $"{folder}/{Guid.NewGuid():N}{ext}";

        stream.Position = 0;
        await minioClient.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length));

        return $"{endpoint}/{bucketName}/{objectName}";
    }

    private async Task<bool> DeleteFromMinIOAsync(string fileUrl)
    {
        try
        {
            var endpoint = _config["Storage:MinIO:Endpoint"];
            var accessKey = _config["Storage:MinIO:AccessKey"];
            var secretKey = _config["Storage:MinIO:SecretKey"];
            var bucketName = _config["Storage:MinIO:BucketName"] ?? "holysafar";

            if (string.IsNullOrEmpty(endpoint)) return false;

            using var minioClient = new MinioClient()
                .WithEndpoint(endpoint)
                .WithCredentials(accessKey, secretKey)
                .Build();

            var uri = new Uri(fileUrl);
            var objectName = uri.AbsolutePath.TrimStart('/').Replace($"{bucketName}/", "");

            await minioClient.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName));
            return true;
        }
        catch { return false; }
    }

    // ==================== HELPERS ====================

    public bool IsImage(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".svg";
    }

    public bool IsDocument(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".txt" or ".csv";
    }
}

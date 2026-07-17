namespace WashUp.Services;

/// <summary>
/// Multi-provider file storage service supporting FileSystem, Azure Blob, S3, and MinIO.
/// </summary>
public class StorageService
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;
    private readonly string _provider;
    private readonly string _basePath;

    public StorageService(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
        _provider = config["FileStorage:Provider"] ?? "FileSystem";
        _basePath = config["FileStorage:BasePath"] ?? "wwwroot/uploads";
    }

    /// <summary>
    /// Upload IFormFile and return the public URL
    /// </summary>
    public async Task<string> UploadAsync(IFormFile file, string? folder = null)
    {
        return _provider switch
        {
            "AzureBlob" => await UploadToAzureAsync(file, folder),
            "S3" or "MinIO" => await UploadToS3Async(file, folder),
            _ => await UploadToFileSystemAsync(file, folder)
        };
    }

    /// <summary>
    /// Upload IBrowserFile (Blazor) and return the public URL
    /// </summary>
    public async Task<string> UploadBrowserFileAsync(Microsoft.AspNetCore.Components.Forms.IBrowserFile file, string? folder = null)
    {
        var uploadPath = Path.Combine(_env.WebRootPath, "uploads", folder ?? "");
        Directory.CreateDirectory(uploadPath);

        var fileName = Guid.NewGuid() + Path.GetExtension(file.Name);
        var filePath = Path.Combine(uploadPath, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024).CopyToAsync(stream);

        return "/uploads/" + (folder != null ? folder + "/" : "") + fileName;
    }

    /// <summary>
    /// Upload file to local filesystem
    /// </summary>
    private async Task<string> UploadToFileSystemAsync(IFormFile file, string? folder)
    {
        var uploadPath = Path.Combine(_env.WebRootPath, "uploads", folder ?? "");
        Directory.CreateDirectory(uploadPath);

        var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(uploadPath, fileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        return "/uploads/" + (folder != null ? folder + "/" : "") + fileName;
    }

    /// <summary>
    /// Upload file to Azure Blob Storage
    /// </summary>
    private async Task<string> UploadToAzureAsync(IFormFile file, string? folder)
    {
        var connectionString = _config["FileStorage:AzureBlob:ConnectionString"];
        var containerName = _config["FileStorage:AzureBlob:ContainerName"] ?? "washup";

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("Azure Blob connection string not configured");

        var blobServiceClient = new Azure.Storage.Blobs.BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync();

        var blobName = (folder != null ? folder + "/" : "") + Guid.NewGuid() + Path.GetExtension(file.FileName);
        var blobClient = containerClient.GetBlobClient(blobName);

        using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, true);

        return blobClient.Uri.ToString();
    }

    /// <summary>
    /// Upload file to S3 or MinIO
    /// </summary>
    private async Task<string> UploadToS3Async(IFormFile file, string? folder)
    {
        var endpoint = _config["FileStorage:S3:Endpoint"];
        var accessKey = _config["FileStorage:S3:AccessKey"];
        var secretKey = _config["FileStorage:S3:SecretKey"];
        var bucketName = _config["FileStorage:S3:BucketName"] ?? "washup";
        var region = _config["FileStorage:S3:Region"] ?? "us-east-1";

        if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
            throw new InvalidOperationException("S3/MinIO credentials not configured");

        var config = new Amazon.S3.AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region),
            ForcePathStyle = true
        };

        if (!string.IsNullOrEmpty(endpoint))
            config.ServiceURL = endpoint;

        using var client = new Amazon.S3.AmazonS3Client(accessKey, secretKey, config);
        
        var key = (folder != null ? folder + "/" : "") + Guid.NewGuid() + Path.GetExtension(file.FileName);
        
        using var stream = file.OpenReadStream();
        var request = new Amazon.S3.Model.PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = stream,
            ContentType = file.ContentType
        };

        await client.PutObjectAsync(request);
        return !string.IsNullOrEmpty(endpoint)
            ? endpoint + "/" + bucketName + "/" + key
            : "https://" + bucketName + ".s3." + region + ".amazonaws.com/" + key;
    }
}

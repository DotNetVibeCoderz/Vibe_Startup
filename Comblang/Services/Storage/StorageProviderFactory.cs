namespace Comblang.Services.Storage;

/// <summary>
/// Factory that builds the correct <see cref="IStorageProvider"/> based on
/// the <c>Storage:Provider</c> configuration key.
/// Supported values: "FileSystem", "S3", "AzureBlob", "MinIO".
/// </summary>
public static class StorageProviderFactory
{
    /// <summary>
    /// Creates an <see cref="IStorageProvider"/> from the application
    /// configuration, reading from the "Storage" section.
    /// </summary>
    public static IStorageProvider Create(IConfiguration configuration, string contentRootPath)
    {
        var provider = configuration["Storage:Provider"] ?? "FileSystem";

        return provider switch
        {
            "AzureBlob" => CreateAzureBlob(configuration),
            "S3" => CreateS3(configuration),
            "MinIO" => CreateMinio(configuration),
            _ => CreateFileSystem(configuration, contentRootPath)
        };
    }

    // ── FileSystem ──────────────────────────────────────────
    private static IStorageProvider CreateFileSystem(IConfiguration config, string root)
    {
        var basePath = config["Storage:BasePath"] ?? "wwwroot/uploads";
        var baseUrl = config["Storage:BaseUrl"] ?? "/uploads";
        var fullPath = Path.Combine(root, basePath);
        return new FileStorageProvider(fullPath, baseUrl);
    }

    // ── Azure Blob ──────────────────────────────────────────
    private static IStorageProvider CreateAzureBlob(IConfiguration config)
    {
        var section = config.GetSection("Storage:AzureBlob");
        var connectionString = section["ConnectionString"]
            ?? throw new InvalidOperationException("Storage:AzureBlob:ConnectionString is required.");
        var container = section["Container"] ?? "comblang";
        var publicUrl = section["PublicUrl"];

        return new AzureBlobStorageProvider(connectionString, container, publicUrl);
    }

    // ── AWS S3 ──────────────────────────────────────────────
    private static IStorageProvider CreateS3(IConfiguration config)
    {
        var section = config.GetSection("Storage:S3");
        var bucket = section["Bucket"] ?? "comblang";
        var region = section["Region"] ?? "us-east-1";
        var accessKey = section["AccessKey"]
            ?? throw new InvalidOperationException("Storage:S3:AccessKey is required.");
        var secretKey = section["SecretKey"]
            ?? throw new InvalidOperationException("Storage:S3:SecretKey is required.");
        var publicUrl = section["PublicUrl"];

        return new S3StorageProvider(bucket, region, accessKey, secretKey, publicUrl);
    }

    // ── MinIO ───────────────────────────────────────────────
    private static IStorageProvider CreateMinio(IConfiguration config)
    {
        var section = config.GetSection("Storage:MinIO");
        var endpoint = section["Endpoint"]
            ?? throw new InvalidOperationException("Storage:MinIO:Endpoint is required.");
        var accessKey = section["AccessKey"]
            ?? throw new InvalidOperationException("Storage:MinIO:AccessKey is required.");
        var secretKey = section["SecretKey"]
            ?? throw new InvalidOperationException("Storage:MinIO:SecretKey is required.");
        var bucket = section["Bucket"] ?? "comblang";
        var useSsl = bool.TryParse(section["UseSsl"], out var ssl) && ssl;
        var publicUrl = section["PublicUrl"];

        return new MinioStorageProvider(endpoint, accessKey, secretKey, bucket, useSsl, publicUrl);
    }
}

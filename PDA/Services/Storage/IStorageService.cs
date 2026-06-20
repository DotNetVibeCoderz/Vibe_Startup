namespace PDA.Services.Storage;

/// <summary>
/// Storage service interface for file operations.
/// Supports: FileSystem, Azure Blob, AWS S3, MinIO
/// </summary>
public interface IStorageService
{
    Task<string> UploadAsync(string fileName, Stream content, string contentType);
    Task<Stream?> DownloadAsync(string filePath);
    Task<bool> DeleteAsync(string filePath);
    Task<string> GetPublicUrlAsync(string filePath);
    Task<bool> ExistsAsync(string filePath);
}

/// <summary>
/// Factory to create the appropriate storage service based on configuration.
/// Registered as SCOPED (same lifetime as the storage services it resolves).
/// </summary>
public class StorageServiceFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StorageServiceFactory> _logger;

    public StorageServiceFactory(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<StorageServiceFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public IStorageService Create()
    {
        var provider = _configuration["Storage:Provider"] ?? "FileSystem";
        _logger.LogInformation("Creating storage service: {Provider}", provider);

        return provider.ToLower() switch
        {
            "azureblob" => _serviceProvider.GetRequiredService<AzureBlobStorageService>(),
            "s3" => _serviceProvider.GetRequiredService<S3StorageService>(),
            "minio" => _serviceProvider.GetRequiredService<MinIOStorageService>(),
            _ => _serviceProvider.GetRequiredService<FileSystemStorageService>()
        };
    }
}

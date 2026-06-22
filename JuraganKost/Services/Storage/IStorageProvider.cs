namespace JuraganKost.Services.Storage;

/// <summary>
/// Unified storage abstraction supporting FileSystem, Azure Blob, AWS S3, and MinIO.
/// </summary>
public interface IStorageProvider
{
    /// <summary>Upload a file and return its public URL</summary>
    Task<string> UploadAsync(string fileName, Stream content, string contentType);

    /// <summary>Delete a file by path/key</summary>
    Task<bool> DeleteAsync(string fileKey);

    /// <summary>Download a file as stream</summary>
    Task<Stream?> DownloadAsync(string fileKey);

    /// <summary>Get public URL for a file</summary>
    string GetPublicUrl(string fileKey);

    /// <summary>Check if a file exists</summary>
    Task<bool> ExistsAsync(string fileKey);

    /// <summary>List files with optional prefix</summary>
    Task<List<string>> ListAsync(string? prefix = null);
}

/// <summary>
/// Configuration model for storage providers
/// </summary>
public class StorageConfig
{
    public string Provider { get; set; } = "FileSystem";

    public FileSystemConfig? FileSystem { get; set; }
    public AzureBlobConfig? AzureBlob { get; set; }
    public S3Config? S3 { get; set; }
    public MinIOConfig? MinIO { get; set; }
}

public class FileSystemConfig { public string Path { get; set; } = "wwwroot/uploads"; }

public class AzureBlobConfig
{
    public string ConnectionString { get; set; } = "";
    public string ContainerName { get; set; } = "juragankost";
}

public class S3Config
{
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string BucketName { get; set; } = "juragankost";
    public string Region { get; set; } = "ap-southeast-1";
    public string? ServiceUrl { get; set; } // For MinIO compatibility
}

public class MinIOConfig
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public string BucketName { get; set; } = "juragankost";
    public bool UseSsl { get; set; }
}

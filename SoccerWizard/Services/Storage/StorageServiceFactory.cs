namespace SoccerWizard.Services.Storage;

/// <summary>
/// Factory untuk memilih storage backend berdasarkan konfigurasi.
/// Supported: FileSystem | S3 | MinIO | AzureBlob
/// </summary>
public static class StorageServiceFactory
{
    public static IStorageService Create(IServiceProvider sp)
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var provider = config["StorageProvider"] ?? "FileSystem";

        return provider switch
        {
            "S3" => ActivatorUtilities.CreateInstance<S3StorageService>(sp),
            "MinIO" => ActivatorUtilities.CreateInstance<MinioStorageService>(sp),
            "AzureBlob" => ActivatorUtilities.CreateInstance<AzureBlobStorageService>(sp),
            _ => ActivatorUtilities.CreateInstance<FileSystemStorageService>(sp)
        };
    }

    public static string GetActiveProvider(IConfiguration config)
        => config["StorageProvider"] ?? "FileSystem";
}

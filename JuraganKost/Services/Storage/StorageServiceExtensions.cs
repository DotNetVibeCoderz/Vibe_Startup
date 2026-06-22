namespace JuraganKost.Services.Storage;

/// <summary>
/// Factory that resolves the appropriate storage provider based on configuration.
/// Usage: inject IStorageProvider via DI, and it will be resolved to the configured provider.
/// </summary>
public static class StorageServiceExtensions
{
    /// <summary>Register the configured storage provider as singleton</summary>
    public static IServiceCollection AddStorageProvider(this IServiceCollection services, IConfiguration config)
    {
        var provider = config.GetValue<string>("StorageProvider") ?? "FileSystem";

        switch (provider)
        {
            case "AzureBlob":
                services.AddSingleton<IStorageProvider, AzureBlobStorageProvider>();
                break;
            case "S3":
                services.AddSingleton<IStorageProvider, S3StorageProvider>();
                break;
            case "MinIO":
                services.AddSingleton<IStorageProvider, MinIOStorageProvider>();
                break;
            default:
                services.AddSingleton<IStorageProvider, FileSystemStorageProvider>();
                break;
        }

        return services;
    }
}

using FastRide.Shared.Storage;

namespace FastRide.Api.Infrastructure;

/// <summary>Creates the storage provider named by <c>Storage:Provider</c>.</summary>
public static class StorageProviderFactory
{
    public static IStorageProvider Create(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();

        return (config["Storage:Provider"] ?? "filesystem").ToLowerInvariant() switch
        {
            "minio" or "s3" => new S3CompatibleStorageProvider(
                config, httpClientFactory, loggerFactory.CreateLogger<S3CompatibleStorageProvider>()),

            "azure" or "azureblob" => new AzureBlobStorageProvider(
                config, httpClientFactory, loggerFactory.CreateLogger<AzureBlobStorageProvider>()),

            _ => new FileSystemStorageProvider(config)
        };
    }
}

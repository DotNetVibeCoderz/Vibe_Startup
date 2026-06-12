using FastRide.Api.Infrastructure;
using FastRide.Shared.Storage;

namespace FastRide.Api.Infrastructure;

/// <summary>Creates storage provider based on configuration.</summary>
public static class StorageProviderFactory
{
    public static IStorageProvider Create(IConfiguration config)
    {
        var provider = config["Storage:Provider"]?.ToLowerInvariant() ?? "filesystem";
        return provider switch
        {
            "minio" or "s3" => new S3CompatibleStorageProvider(config),
            "azure" or "azureblob" => new AzureBlobStorageProvider(config),
            _ => new FileSystemStorageProvider(config),
        };
    }
}

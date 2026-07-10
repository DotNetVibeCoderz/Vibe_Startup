namespace EstateHub.Services.Storage;

/// <summary>
/// Factory that resolves the active storage provider based on configuration
/// </summary>
public class StorageProviderFactory
{
    private readonly IConfiguration _config;
    private readonly IServiceProvider _serviceProvider;

    public StorageProviderFactory(IConfiguration config, IServiceProvider serviceProvider)
    {
        _config = config;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Get the configured storage provider. Default: FileSystem
    /// </summary>
    public IStorageProvider GetProvider()
    {
        var provider = _config.GetValue<string>("StorageProvider") ?? "FileSystem";
        return provider.ToLower() switch
        {
            "azureblob" => GetService<AzureBlobStorageProvider>(),
            "s3" => GetService<S3StorageProvider>(),
            "minio" => GetService<MinIOStorageProvider>(),
            _ => GetService<FileSystemStorageProvider>()
        };
    }

    private T GetService<T>() where T : IStorageProvider
    {
        return _serviceProvider.GetRequiredService<T>();
    }
}

/// <summary>
/// Service registration helper
/// </summary>
public static class StorageExtensions
{
    public static IServiceCollection AddStorageProviders(this IServiceCollection services)
    {
        services.AddScoped<FileSystemStorageProvider>();
        services.AddScoped<AzureBlobStorageProvider>();
        services.AddScoped<S3StorageProvider>();
        services.AddScoped<MinIOStorageProvider>();
        services.AddScoped<StorageProviderFactory>();
        return services;
    }
}

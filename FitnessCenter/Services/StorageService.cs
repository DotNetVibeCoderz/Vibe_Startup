using FitnessCenter.Services.Storage;

namespace FitnessCenter.Services;

/// <summary>
/// Facade service untuk storage - mendukung FileSystem, Azure Blob, AWS S3, dan MinIO.
/// Provider dipilih berdasarkan konfigurasi di appsettings.json.
/// </summary>
public class StorageService
{
    private readonly IStorageProvider _provider;
    private readonly ILogger<StorageService> _logger;

    /// <summary>Provider yang sedang aktif</summary>
    public string ActiveProvider => _provider.ProviderName;

    public StorageService(IConfiguration config, IWebHostEnvironment env, ILogger<StorageService> logger)
    {
        _logger = logger;
        _provider = CreateProvider(config, env);
        _logger.LogInformation("Storage provider initialized: {Provider}", _provider.ProviderName);
    }

    /// <summary>
    /// Factory method: membuat storage provider berdasarkan konfigurasi.
    /// </summary>
    private static IStorageProvider CreateProvider(IConfiguration config, IWebHostEnvironment env)
    {
        var providerName = config.GetValue<string>("Storage:Provider") ?? "FileSystem";

        try
        {
            return providerName.ToLowerInvariant() switch
            {
                "azureblob" or "azure" => new AzureBlobStorageProvider(config),
                "s3" or "aws" => new S3StorageProvider(config),
                "minio" => new MinIOStorageProvider(config),
                "filesystem" or "local" or _ => new FileSystemStorageProvider(config, env)
            };
        }
        catch (Exception ex)
        {
            // Fallback ke FileSystem jika provider gagal diinisialisasi
            Console.WriteLine($"Warning: Failed to initialize '{providerName}' provider: {ex.Message}. Falling back to FileSystem.");
            return new FileSystemStorageProvider(config, env);
        }
    }

    /// <summary>Upload file stream dan return URL</summary>
    /// <param name="fileStream">File data stream</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="folder">Optional subfolder</param>
    /// <param name="contentType">Optional MIME type</param>
    /// <returns>Public URL to the uploaded file</returns>
    public async Task<string> UploadAsync(Stream fileStream, string fileName, string folder = "", string? contentType = null)
    {
        if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentNullException(nameof(fileName));

        try
        {
            _logger.LogDebug("Uploading '{FileName}' to {Provider}/{Folder}", fileName, _provider.ProviderName, folder);
            var url = await _provider.UploadAsync(fileStream, fileName, folder, contentType);
            _logger.LogInformation("File uploaded: {Url}", url);
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file '{FileName}'", fileName);
            throw new InvalidOperationException($"Failed to upload file: {ex.Message}", ex);
        }
    }

    /// <summary>Upload dari base64 string</summary>
    public async Task<string> UploadBase64Async(string base64, string fileName, string folder = "")
    {
        if (string.IsNullOrWhiteSpace(base64)) throw new ArgumentNullException(nameof(base64));

        try
        {
            return await _provider.UploadBase64Async(base64, fileName, folder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload base64 file '{FileName}'", fileName);
            throw new InvalidOperationException($"Failed to upload base64 file: {ex.Message}", ex);
        }
    }

    /// <summary>Upload dari byte array</summary>
    public async Task<string> UploadBytesAsync(byte[] bytes, string fileName, string folder = "", string? contentType = null)
    {
        using var ms = new MemoryStream(bytes);
        return await UploadAsync(ms, fileName, folder, contentType);
    }

    /// <summary>Upload dari file path lokal</summary>
    public async Task<string> UploadFromPathAsync(string localFilePath, string? customFileName = null, string folder = "")
    {
        if (!File.Exists(localFilePath)) throw new FileNotFoundException("Local file not found", localFilePath);

        var fileName = customFileName ?? Path.GetFileName(localFilePath);
        await using var fs = File.OpenRead(localFilePath);
        return await UploadAsync(fs, fileName, folder);
    }

    /// <summary>Hapus file</summary>
    public async Task<bool> DeleteAsync(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return false;

        try
        {
            return await _provider.DeleteAsync(fileUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {Url}", fileUrl);
            return false;
        }
    }

    /// <summary>Cek apakah file exists</summary>
    public async Task<bool> ExistsAsync(string fileUrl)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return false;

        try
        {
            return await _provider.ExistsAsync(fileUrl);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Batch delete files</summary>
    public async Task<int> DeleteManyAsync(IEnumerable<string> fileUrls)
    {
        var tasks = fileUrls.Select(url => DeleteAsync(url));
        var results = await Task.WhenAll(tasks);
        return results.Count(r => r);
    }

    /// <summary>
    /// Dapatkan informasi provider yang sedang aktif
    /// </summary>
    public StorageInfo GetStorageInfo()
    {
        return new StorageInfo
        {
            ProviderName = _provider.ProviderName,
            IsFileSystem = _provider is FileSystemStorageProvider,
            IsCloudStorage = _provider is not FileSystemStorageProvider
        };
    }
}

/// <summary>
/// Informasi tentang storage provider yang aktif
/// </summary>
public class StorageInfo
{
    public string ProviderName { get; set; } = string.Empty;
    public bool IsFileSystem { get; set; }
    public bool IsCloudStorage { get; set; }
}

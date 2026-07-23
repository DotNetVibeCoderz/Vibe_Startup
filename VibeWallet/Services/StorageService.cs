using Microsoft.Extensions.Options;
using VibeWallet.Models;

namespace VibeWallet.Services;

/// <summary>
/// Implementation of storage service with multiple provider support
/// Supports: FileSystem, AzureBlob, S3, MinIO
/// </summary>
public class StorageService : IStorageService
{
    private readonly StorageConfig _config;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<StorageService> _logger;

    public StorageService(IOptions<StorageConfig> config, IWebHostEnvironment env,
        ILogger<StorageService> logger)
    {
        _config = config.Value;
        _env = env;
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, string folder = "")
    {
        return _config.Provider switch
        {
            "AzureBlob" => await UploadToAzureBlobAsync(fileStream, fileName, contentType, folder),
            "S3" => await UploadToS3Async(fileStream, fileName, contentType, folder),
            "MinIO" => await UploadToMinIOAsync(fileStream, fileName, contentType, folder),
            _ => await UploadToFileSystemAsync(fileStream, fileName, folder)
        };
    }

    public async Task<Stream?> DownloadFileAsync(string fileUrl)
    {
        return _config.Provider switch
        {
            "AzureBlob" => await DownloadFromAzureBlobAsync(fileUrl),
            "S3" => await DownloadFromS3Async(fileUrl),
            "MinIO" => await DownloadFromMinIOAsync(fileUrl),
            _ => await DownloadFromFileSystemAsync(fileUrl)
        };
    }

    public async Task<bool> DeleteFileAsync(string fileUrl)
    {
        try
        {
            return _config.Provider switch
            {
                "AzureBlob" => await DeleteFromAzureBlobAsync(fileUrl),
                "S3" => await DeleteFromS3Async(fileUrl),
                "MinIO" => await DeleteFromMinIOAsync(fileUrl),
                _ => await DeleteFromFileSystemAsync(fileUrl)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {Url}", fileUrl);
            return false;
        }
    }

    public Task<string> GetFileUrlAsync(string fileName, string folder = "")
    {
        var basePath = _config.FileSystem?.BasePath ?? "wwwroot/uploads";
        var relativePath = string.IsNullOrEmpty(folder) ? fileName : $"{folder}/{fileName}";
        var fullUrl = $"/{basePath.Replace("wwwroot/", "")}/{relativePath}";
        return Task.FromResult(fullUrl);
    }

    public Task<bool> FileExistsAsync(string fileUrl)
    {
        if (_config.Provider == "FileSystem" || string.IsNullOrEmpty(_config.Provider))
        {
            var filePath = Path.Combine(_env.WebRootPath ?? "wwwroot", fileUrl.TrimStart('/'));
            return Task.FromResult(File.Exists(filePath));
        }
        return Task.FromResult(true); // Assume exists for cloud storage
    }

    public async Task<long> GetFileSizeAsync(string fileUrl)
    {
        if (_config.Provider == "FileSystem" || string.IsNullOrEmpty(_config.Provider))
        {
            var filePath = Path.Combine(_env.WebRootPath ?? "wwwroot", fileUrl.TrimStart('/'));
            if (File.Exists(filePath))
                return new FileInfo(filePath).Length;
        }
        return 0;
    }

    // ===== FileSystem Implementation =====
    private async Task<string> UploadToFileSystemAsync(Stream fileStream, string fileName, string folder)
    {
        var basePath = Path.Combine(_env.WebRootPath ?? "wwwroot",
            _config.FileSystem?.BasePath ?? "uploads");

        var uploadFolder = string.IsNullOrEmpty(folder) ? basePath : Path.Combine(basePath, folder);
        Directory.CreateDirectory(uploadFolder);

        var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";
        var filePath = Path.Combine(uploadFolder, uniqueFileName);

        using (var fileStream2 = File.Create(filePath))
        {
            await fileStream.CopyToAsync(fileStream2);
        }

        var relativeUrl = $"/{(_config.FileSystem?.BasePath ?? "uploads").Replace("wwwroot/", "")}/{(string.IsNullOrEmpty(folder) ? "" : folder + "/")}{uniqueFileName}";
        return relativeUrl;
    }

    private Task<Stream?> DownloadFromFileSystemAsync(string fileUrl)
    {
        var filePath = Path.Combine(_env.WebRootPath ?? "wwwroot", fileUrl.TrimStart('/'));
        if (!File.Exists(filePath)) return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(File.OpenRead(filePath));
    }

    private Task<bool> DeleteFromFileSystemAsync(string fileUrl)
    {
        var filePath = Path.Combine(_env.WebRootPath ?? "wwwroot", fileUrl.TrimStart('/'));
        if (!File.Exists(filePath)) return Task.FromResult(false);

        File.Delete(filePath);
        return Task.FromResult(true);
    }

    // ===== Azure Blob Placeholder =====
    private async Task<string> UploadToAzureBlobAsync(Stream fileStream, string fileName, string contentType, string folder)
    {
        _logger.LogWarning("AzureBlob storage not fully implemented. Falling back to FileSystem.");
        return await UploadToFileSystemAsync(fileStream, fileName, folder);
    }

    private Task<Stream?> DownloadFromAzureBlobAsync(string fileUrl)
    {
        return DownloadFromFileSystemAsync(fileUrl);
    }

    private Task<bool> DeleteFromAzureBlobAsync(string fileUrl)
    {
        return DeleteFromFileSystemAsync(fileUrl);
    }

    // ===== S3 Placeholder =====
    private async Task<string> UploadToS3Async(Stream fileStream, string fileName, string contentType, string folder)
    {
        _logger.LogWarning("S3 storage not fully implemented. Falling back to FileSystem.");
        return await UploadToFileSystemAsync(fileStream, fileName, folder);
    }

    private Task<Stream?> DownloadFromS3Async(string fileUrl)
    {
        return DownloadFromFileSystemAsync(fileUrl);
    }

    private Task<bool> DeleteFromS3Async(string fileUrl)
    {
        return DeleteFromFileSystemAsync(fileUrl);
    }

    // ===== MinIO Placeholder =====
    private async Task<string> UploadToMinIOAsync(Stream fileStream, string fileName, string contentType, string folder)
    {
        _logger.LogWarning("MinIO storage not fully implemented. Falling back to FileSystem.");
        return await UploadToFileSystemAsync(fileStream, fileName, folder);
    }

    private Task<Stream?> DownloadFromMinIOAsync(string fileUrl)
    {
        return DownloadFromFileSystemAsync(fileUrl);
    }

    private Task<bool> DeleteFromMinIOAsync(string fileUrl)
    {
        return DeleteFromFileSystemAsync(fileUrl);
    }
}

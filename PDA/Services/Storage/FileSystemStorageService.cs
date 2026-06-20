namespace PDA.Services.Storage;

/// <summary>
/// File system based storage service.
/// Stores files locally under wwwroot/uploads or configured path.
/// </summary>
public class FileSystemStorageService : IStorageService
{
    private readonly string _basePath;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FileSystemStorageService> _logger;

    public FileSystemStorageService(IConfiguration configuration, IWebHostEnvironment env, ILogger<FileSystemStorageService> logger)
    {
        _env = env;
        _logger = logger;
        _basePath = configuration["Storage:FileSystem:BasePath"] ?? "wwwroot/uploads";
    }

    /// <summary>
    /// Upload file to local file system with unique filename
    /// </summary>
    public Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        try
        {
            var uniqueName = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(fileName)}";
            var fullPath = GetFullPath(uniqueName);

            var dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir) && dir != null)
                Directory.CreateDirectory(dir);

            using var fileStream = new FileStream(fullPath, FileMode.Create);
            content.CopyTo(fileStream);

            _logger.LogInformation("File uploaded: {FileName} -> {Path}", fileName, fullPath);
            return Task.FromResult(uniqueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload: {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Download file from local file system
    /// </summary>
    public Task<Stream?> DownloadAsync(string filePath)
    {
        var fullPath = GetFullPath(filePath);
        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    /// <summary>
    /// Delete file from local file system
    /// </summary>
    public Task<bool> DeleteAsync(string filePath)
    {
        var fullPath = GetFullPath(filePath);
        if (!File.Exists(fullPath))
            return Task.FromResult(false);

        File.Delete(fullPath);
        _logger.LogInformation("File deleted: {Path}", fullPath);
        return Task.FromResult(true);
    }

    /// <summary>
    /// Get relative public URL for file
    /// </summary>
    public Task<string> GetPublicUrlAsync(string filePath)
    {
        var url = $"/uploads/{filePath.Replace("\\", "/")}";
        return Task.FromResult(url);
    }

    /// <summary>
    /// Check if file exists
    /// </summary>
    public Task<bool> ExistsAsync(string filePath)
    {
        return Task.FromResult(File.Exists(GetFullPath(filePath)));
    }

    private string GetFullPath(string relativePath)
    {
        return Path.Combine(_env.WebRootPath, "uploads", relativePath);
    }
}

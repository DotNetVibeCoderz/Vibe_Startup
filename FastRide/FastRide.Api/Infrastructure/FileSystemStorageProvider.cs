using FastRide.Shared.Storage;

namespace FastRide.Api.Infrastructure;

/// <summary>Local file system storage. Saves files to a configurable directory and serves via /uploads/.</summary>
public class FileSystemStorageProvider : IStorageProvider
{
    private readonly string _basePath;
    private readonly string _baseUrl;

    public FileSystemStorageProvider(IConfiguration config)
    {
        _basePath = config["Storage:FileSystem:Path"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _baseUrl = config["Storage:FileSystem:BaseUrl"] ?? "/uploads";
        Directory.CreateDirectory(_basePath);
    }

    public Task<string> UploadAsync(string fileName, byte[] data, string contentType, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, fileName);
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(filePath, data);
        return Task.FromResult($"{_baseUrl}/{fileName}");
    }

    public Task<byte[]?> DownloadAsync(string fileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, fileName);
        return Task.FromResult(File.Exists(filePath) ? File.ReadAllBytes(filePath) : null);
    }

    public Task<bool> DeleteAsync(string fileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, fileName);
        if (!File.Exists(filePath)) return Task.FromResult(false);
        File.Delete(filePath);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string fileName, CancellationToken ct = default)
        => Task.FromResult(File.Exists(Path.Combine(_basePath, fileName)));

    public string GeneratePhotoFileName(Guid userId, string extension)
        => $"photos/{userId.ToString("N")[..12]}_{DateTime.UtcNow:yyyyMMddHHmmss}.{extension.TrimStart('.')}";
}

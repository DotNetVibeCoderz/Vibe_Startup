namespace JuraganKost.Services.Storage;

/// <summary>
/// FileSystem storage provider - default for local development.
/// Stores files in the local filesystem under wwwroot/uploads.
/// </summary>
public class FileSystemStorageProvider : IStorageProvider
{
    private readonly string _rootPath;
    private readonly string _baseUrl;

    public FileSystemStorageProvider(IConfiguration config, IWebHostEnvironment env)
    {
        var fsConfig = config.GetSection("StorageConfig:FileSystem");
        _rootPath = fsConfig.GetValue<string>("Path") ?? "wwwroot/uploads";

        // Ensure absolute path
        if (!Path.IsPathRooted(_rootPath))
            _rootPath = Path.Combine(env.ContentRootPath, _rootPath);

        Directory.CreateDirectory(_rootPath);
        _baseUrl = "/uploads";
    }

    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        // Generate unique file name to avoid collisions
        var ext = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(_rootPath, uniqueName);

        await using var fs = File.Create(filePath);
        await content.CopyToAsync(fs);

        return $"{_baseUrl}/{uniqueName}";
    }

    public Task<bool> DeleteAsync(string fileKey)
    {
        var fileName = Path.GetFileName(new Uri(fileKey).AbsolutePath);
        var filePath = Path.Combine(_rootPath, fileName);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<Stream?> DownloadAsync(string fileKey)
    {
        var fileName = Path.GetFileName(new Uri(fileKey).AbsolutePath);
        var filePath = Path.Combine(_rootPath, fileName);

        if (File.Exists(filePath))
            return Task.FromResult<Stream?>(File.OpenRead(filePath));
        return Task.FromResult<Stream?>(null);
    }

    public string GetPublicUrl(string fileKey)
    {
        // fileKey is already a relative URL from UploadAsync
        return fileKey;
    }

    public Task<bool> ExistsAsync(string fileKey)
    {
        var fileName = Path.GetFileName(new Uri(fileKey).AbsolutePath);
        return Task.FromResult(File.Exists(Path.Combine(_rootPath, fileName)));
    }

    public Task<List<string>> ListAsync(string? prefix = null)
    {
        var files = Directory.GetFiles(_rootPath)
            .Select(Path.GetFileName)
            .Select(f => $"{_baseUrl}/{f}")
            .Where(f => prefix == null || f.Contains(prefix!))
            .ToList();
        return Task.FromResult(files);
    }
}

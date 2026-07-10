namespace EstateHub.Services.Storage;

/// <summary>
/// Local file system storage provider (default)
/// Stores files in wwwroot/uploads/
/// </summary>
public class FileSystemStorageProvider : IStorageProvider
{
    private readonly string _basePath;
    private readonly string _baseUrl;

    public string ProviderName => "FileSystem";

    public FileSystemStorageProvider(IConfiguration config)
    {
        _basePath = config.GetValue<string>("Storage:FileSystem:BasePath") ?? "wwwroot/uploads";
        _baseUrl = "/uploads";
        Directory.CreateDirectory(_basePath);
    }

    public async Task<StorageResult> UploadAsync(Stream stream, string fileName, string contentType, string subfolder = "")
    {
        try
        {
            var folder = string.IsNullOrEmpty(subfolder) ? _basePath : Path.Combine(_basePath, subfolder);
            Directory.CreateDirectory(folder);

            var uniqueName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
            var filePath = Path.Combine(folder, uniqueName);

            using var fileStream = new FileStream(filePath, FileMode.Create);
            await stream.CopyToAsync(fileStream);

            var url = string.IsNullOrEmpty(subfolder)
                ? $"{_baseUrl}/{uniqueName}"
                : $"{_baseUrl}/{subfolder}/{uniqueName}";

            return StorageResult.Ok(url, uniqueName, fileStream.Length);
        }
        catch (Exception ex)
        {
            return StorageResult.Fail($"FileSystem upload failed: {ex.Message}");
        }
    }

    public async Task<StorageResult> UploadAsync(byte[] data, string fileName, string contentType, string subfolder = "")
    {
        using var stream = new MemoryStream(data);
        return await UploadAsync(stream, fileName, contentType, subfolder);
    }

    public Task<bool> DeleteAsync(string fileUrl)
    {
        try
        {
            var relativePath = fileUrl.Replace(_baseUrl, "").TrimStart('/');
            var fullPath = Path.Combine(_basePath, relativePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> ExistsAsync(string fileUrl)
    {
        var relativePath = fileUrl.Replace(_baseUrl, "").TrimStart('/');
        var fullPath = Path.Combine(_basePath, relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public string GetPublicUrl(string fileName, string subfolder = "")
    {
        return string.IsNullOrEmpty(subfolder)
            ? $"{_baseUrl}/{fileName}"
            : $"{_baseUrl}/{subfolder}/{fileName}";
    }

    public async Task<Stream?> DownloadAsync(string fileUrl)
    {
        var relativePath = fileUrl.Replace(_baseUrl, "").TrimStart('/');
        var fullPath = Path.Combine(_basePath, relativePath);
        if (!File.Exists(fullPath)) return null;
        return await Task.FromResult(new FileStream(fullPath, FileMode.Open, FileAccess.Read));
    }

    public Task<List<string>> ListFilesAsync(string subfolder = "", int maxResults = 100)
    {
        var folder = string.IsNullOrEmpty(subfolder) ? _basePath : Path.Combine(_basePath, subfolder);
        if (!Directory.Exists(folder)) return Task.FromResult(new List<string>());

        var files = Directory.GetFiles(folder)
            .Take(maxResults)
            .Select(f => f.Replace(_basePath, _baseUrl).Replace("\\", "/"))
            .ToList();
        return Task.FromResult(files);
    }
}

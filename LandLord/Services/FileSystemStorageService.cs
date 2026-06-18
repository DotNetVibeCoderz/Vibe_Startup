namespace LandLord.Services;

/// <summary>
/// Implementasi penyimpanan file menggunakan file system lokal
/// </summary>
public class FileSystemStorageService : IStorageService
{
    private readonly string _basePath;
    private readonly string _baseUrl;
    private readonly IWebHostEnvironment _env;
    private readonly long _maxFileSize;
    private readonly string[] _allowedExtensions;

    public string ProviderName => "FileSystem";

    public FileSystemStorageService(IConfiguration configuration, IWebHostEnvironment env)
    {
        _env = env;
        _basePath = configuration.GetValue<string>("StorageProvider:BasePath") ?? "wwwroot/uploads";
        _baseUrl = configuration.GetValue<string>("StorageProvider:BaseUrl") ?? "/uploads";
        _maxFileSize = configuration.GetValue<long>("StorageProvider:MaxFileSizeMB", 50) * 1024 * 1024;

        var extensions = configuration.GetSection("StorageProvider:AllowedExtensions").Get<string[]>();
        _allowedExtensions = extensions ?? new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".mp4", ".doc", ".docx", ".xls", ".xlsx" };

        // Pastikan direktori ada
        var fullPath = Path.Combine(env.ContentRootPath, _basePath);
        if (!Directory.Exists(fullPath))
            Directory.CreateDirectory(fullPath);
    }

    public async Task<string> UploadAsync(string fileName, Stream fileStream, string contentType)
    {
        // Validasi ekstensi
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"Ekstensi file '{extension}' tidak diizinkan. Ekstensi yang diizinkan: {string.Join(", ", _allowedExtensions)}");
        }

        // Generate nama file unik
        var uniqueName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine(_basePath, uniqueName);
        var fullPath = Path.Combine(_env.ContentRootPath, relativePath);

        // Pastikan direktori target ada
        var directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory) && directory != null)
            Directory.CreateDirectory(directory);

        // Simpan file
        await using var fs = new FileStream(fullPath, FileMode.Create);
        await fileStream.CopyToAsync(fs);

        return relativePath;
    }

    public Task<Stream?> DownloadAsync(string filePath)
    {
        var fullPath = Path.Combine(_env.ContentRootPath, filePath);
        if (!File.Exists(fullPath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(new FileStream(fullPath, FileMode.Open, FileAccess.Read));
    }

    public Task<bool> DeleteAsync(string filePath)
    {
        var fullPath = Path.Combine(_env.ContentRootPath, filePath);
        if (!File.Exists(fullPath))
            return Task.FromResult(false);

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    public Task<string> GetPublicUrlAsync(string filePath)
    {
        // Untuk file system, kembali ke relative URL
        var relativeUrl = filePath
            .Replace("wwwroot/", "")
            .Replace("\\", "/");

        return Task.FromResult($"/{relativeUrl.TrimStart('/')}");
    }

    public Task<bool> FileExistsAsync(string filePath)
    {
        var fullPath = Path.Combine(_env.ContentRootPath, filePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task<StorageFileInfo?> GetFileInfoAsync(string filePath)
    {
        var fullPath = Path.Combine(_env.ContentRootPath, filePath);
        if (!File.Exists(fullPath))
            return Task.FromResult<StorageFileInfo?>(null);

        var fileInfo = new FileInfo(fullPath);
        var result = new StorageFileInfo
        {
            FileName = fileInfo.Name,
            FilePath = filePath,
            FileSize = fileInfo.Length,
            ContentType = GetMimeType(fileInfo.Extension),
            LastModified = fileInfo.LastWriteTimeUtc,
            PublicUrl = $"/{filePath.Replace("wwwroot/", "").Replace("\\", "/").TrimStart('/')}"
        };

        return Task.FromResult<StorageFileInfo?>(result);
    }

    public Task<List<StorageFileInfo>> ListFilesAsync(string? prefix = null, int maxResults = 100)
    {
        var searchPath = Path.Combine(_env.ContentRootPath, _basePath);
        if (!string.IsNullOrEmpty(prefix))
            searchPath = Path.Combine(searchPath, prefix);

        if (!Directory.Exists(searchPath))
            return Task.FromResult(new List<StorageFileInfo>());

        var files = Directory.GetFiles(searchPath, "*.*", SearchOption.AllDirectories)
            .Take(maxResults)
            .Select(f =>
            {
                var fi = new FileInfo(f);
                var relativePath = Path.GetRelativePath(_env.ContentRootPath, f);
                return new StorageFileInfo
                {
                    FileName = fi.Name,
                    FilePath = relativePath,
                    FileSize = fi.Length,
                    ContentType = GetMimeType(fi.Extension),
                    LastModified = fi.LastWriteTimeUtc,
                    PublicUrl = $"/{relativePath.Replace("wwwroot/", "").Replace("\\", "/").TrimStart('/')}"
                };
            })
            .ToList();

        return Task.FromResult(files);
    }

    public Task<bool> CheckConnectionAsync()
    {
        try
        {
            var fullPath = Path.Combine(_env.ContentRootPath, _basePath);
            if (!Directory.Exists(fullPath))
                Directory.CreateDirectory(fullPath);

            // Test baca/tulis
            var testFile = Path.Combine(fullPath, ".storage-test");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static string GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
}

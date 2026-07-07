namespace FitnessCenter.Services.Storage;

/// <summary>
/// Local file system storage provider.
/// Menyimpan file di folder lokal dan menyajikan via static files.
/// </summary>
public class FileSystemStorageProvider : IStorageProvider
{
    private readonly string _webRoot;
    private readonly string _rootPath; // relatif terhadap web root, tanpa "wwwroot" prefix
    private readonly string _baseUrl;

    public string ProviderName => "FileSystem";

    public FileSystemStorageProvider(IConfiguration config, IWebHostEnvironment env)
    {
        // WebRootPath bisa null di env tertentu, fallback ke "wwwroot"
        _webRoot = env.WebRootPath
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

        // Baca dari config, lalu strip "wwwroot/" prefix jika ada
        var configuredPath = config.GetValue<string>("Storage:FileSystem:RootPath") ?? "wwwroot/uploads";
        _rootPath = configuredPath.Replace("wwwroot/", "").Replace("wwwroot\\", "").TrimStart('/', '\\');

        // Base URL untuk akses publik
        _baseUrl = "/" + _rootPath.Replace("\\", "/");
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string folder = "", string? contentType = null)
    {
        // Path absolut ke folder target
        var uploadDir = string.IsNullOrEmpty(folder)
            ? Path.Combine(_webRoot, _rootPath)
            : Path.Combine(_webRoot, _rootPath, folder);

        // Buat direktori jika belum ada
        Directory.CreateDirectory(uploadDir);

        // Generate nama unik untuk menghindari konflik
        var extension = Path.GetExtension(fileName);
        var safeName = SanitizeFileName(Path.GetFileNameWithoutExtension(fileName));
        var uniqueName = $"{Guid.NewGuid():N}_{safeName}{extension}";
        var filePath = Path.Combine(uploadDir, uniqueName);

        // Tulis file
        await using var fs = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(fs);
        await fs.FlushAsync();

        // Return relative URL (bisa langsung diakses via static files)
        var url = string.IsNullOrEmpty(folder)
            ? $"{_baseUrl}/{uniqueName}"
            : $"{_baseUrl}/{folder}/{uniqueName}";

        return url;
    }

    public Task<string> UploadBase64Async(string base64, string fileName, string folder = "")
    {
        var cleanBase64 = base64.Contains(',') ? base64[(base64.IndexOf(',') + 1)..] : base64;
        var bytes = Convert.FromBase64String(cleanBase64);
        using var ms = new MemoryStream(bytes);
        return UploadAsync(ms, fileName, folder);
    }

    public Task<bool> DeleteAsync(string fileUrl)
    {
        try
        {
            // Strip base URL prefix untuk dapat path relatif ke webroot
            var relativePath = fileUrl.TrimStart('/');
            var fullPath = Path.Combine(_webRoot, relativePath);

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
        var relativePath = fileUrl.TrimStart('/');
        var fullPath = Path.Combine(_webRoot, relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    public string GetPublicUrl(string fileKey, string folder)
    {
        return string.IsNullOrEmpty(folder)
            ? $"{_baseUrl}/{fileKey}"
            : $"{_baseUrl}/{folder}/{fileKey}";
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "file" : sanitized;
    }
}

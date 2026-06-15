namespace SoccerWizard.Services.Storage;

/// <summary>
/// Storage backend: Local File System.
/// File disimpan di folder wwwroot/uploads.
/// </summary>
public class FileSystemStorageService : IStorageService
{
    private readonly string _rootPath;
    public string ProviderName => "FileSystem";

    public FileSystemStorageService(IConfiguration config)
    {
        _rootPath = Path.Combine(Directory.GetCurrentDirectory(),
            config["Storage:FileSystem:RootPath"] ?? "wwwroot/uploads");
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> UploadAsync(string fileName, Stream stream, string contentType)
    {
        var uniqueName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var fullPath = Path.Combine(_rootPath, uniqueName);

        await using var fs = new FileStream(fullPath, FileMode.Create);
        await stream.CopyToAsync(fs);

        return $"/uploads/{uniqueName}";
    }

    public Task<Stream?> DownloadAsync(string fileName)
    {
        var path = ResolvePath(fileName);
        if (!File.Exists(path)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(path));
    }

    public Task DeleteAsync(string fileName)
    {
        var path = ResolvePath(fileName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string fileName)
        => Task.FromResult(File.Exists(ResolvePath(fileName)));

    public string GetPublicUrl(string fileName)
        => $"/uploads/{fileName}";

    private string ResolvePath(string fileName)
        => Path.Combine(_rootPath, Path.GetFileName(fileName));
}

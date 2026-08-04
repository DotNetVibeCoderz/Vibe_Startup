using FastRide.Shared.Storage;

namespace FastRide.Api.Infrastructure;

/// <summary>
/// Local file system storage. Saves files under a configurable directory and serves them
/// as static files from <c>Storage:FileSystem:BaseUrl</c> (default <c>/uploads</c>).
/// </summary>
public sealed class FileSystemStorageProvider : IStorageProvider
{
    private readonly string _basePath;
    private readonly string _baseUrl;

    public string Name => "FileSystem";

    public FileSystemStorageProvider(IConfiguration config)
    {
        var configured = config["Storage:FileSystem:Path"];
        _basePath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Directory.GetCurrentDirectory(), "uploads")
            : Path.GetFullPath(configured, Directory.GetCurrentDirectory());

        _baseUrl = (config["Storage:FileSystem:BaseUrl"] ?? "/uploads").TrimEnd('/');
        Directory.CreateDirectory(_basePath);
    }

    /// <summary>Root directory, so the host can map it as a static file provider.</summary>
    public string RootPath => _basePath;

    /// <summary>URL prefix the root directory is served under.</summary>
    public string RequestPath => _baseUrl;

    public async Task<string> UploadAsync(string fileName, byte[] data, string contentType, CancellationToken ct = default)
    {
        var filePath = ResolveSafePath(fileName);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(filePath, data, ct);
        return $"{_baseUrl}/{fileName.Replace('\\', '/')}";
    }

    public async Task<byte[]?> DownloadAsync(string fileName, CancellationToken ct = default)
    {
        var filePath = ResolveSafePath(fileName);
        return File.Exists(filePath) ? await File.ReadAllBytesAsync(filePath, ct) : null;
    }

    public Task<bool> DeleteAsync(string fileName, CancellationToken ct = default)
    {
        var filePath = ResolveSafePath(fileName);
        if (!File.Exists(filePath)) return Task.FromResult(false);

        File.Delete(filePath);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsAsync(string fileName, CancellationToken ct = default)
        => Task.FromResult(File.Exists(ResolveSafePath(fileName)));

    public string? ResolveFileName(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        // Generated avatars are inline data: URIs — there is no stored file behind them.
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return null;

        var path = url;
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute)) path = absolute.AbsolutePath;

        return path.StartsWith(_baseUrl + "/", StringComparison.OrdinalIgnoreCase)
            ? path[(_baseUrl.Length + 1)..]
            : null;
    }

    /// <summary>
    /// Keeps a crafted name such as <c>../../appsettings.json</c> from escaping the upload root.
    /// </summary>
    private string ResolveSafePath(string fileName)
    {
        var combined = Path.GetFullPath(Path.Combine(_basePath, fileName));
        var root = _basePath.EndsWith(Path.DirectorySeparatorChar) ? _basePath : _basePath + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Resolved storage path escapes the upload directory.");

        return combined;
    }
}

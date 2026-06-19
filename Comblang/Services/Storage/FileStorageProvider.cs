namespace Comblang.Services.Storage;

/// <summary>
/// Local file-system storage provider. Files are stored under a configurable
/// base path served as static files (e.g. wwwroot/uploads).
/// </summary>
public class FileStorageProvider : IStorageProvider
{
    private readonly string _basePath;
    private readonly string _baseUrl;

    /// <summary>
    /// Creates a new FileStorageProvider.
    /// </summary>
    /// <param name="basePath">Absolute path to the storage root folder.</param>
    /// <param name="baseUrl">Public URL prefix that maps to <paramref name="basePath"/>.</param>
    public FileStorageProvider(string basePath, string baseUrl)
    {
        _basePath = Path.GetFullPath(basePath);
        _baseUrl = baseUrl.TrimEnd('/');

        if (!Directory.Exists(_basePath))
            Directory.CreateDirectory(_basePath);
    }

    /// <inheritdoc />
    public Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        var sanitised = SanitiseFileName(fileName);
        var filePath = Path.Combine(_basePath, sanitised);
        var dir = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var fileStream = File.Create(filePath);
        content.CopyTo(fileStream);

        var publicUrl = BuildPublicUrl(sanitised);
        return Task.FromResult(publicUrl);
    }

    /// <inheritdoc />
    public Task<Stream?> DownloadAsync(string fileName)
    {
        var filePath = Path.Combine(_basePath, SanitiseFileName(fileName));

        if (!File.Exists(filePath))
            return Task.FromResult<Stream?>(null);

        // Open with read sharing so other processes can also read
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string fileName)
    {
        var filePath = Path.Combine(_basePath, SanitiseFileName(fileName));

        if (!File.Exists(filePath))
            return Task.FromResult(false);

        File.Delete(filePath);

        // Clean up empty parent directories (optional safety measure)
        var dir = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(dir) && dir.StartsWith(_basePath) && dir != _basePath)
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
            else
                break;

            dir = Path.GetDirectoryName(dir);
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<string> GetPublicUrlAsync(string fileName)
    {
        var sanitised = SanitiseFileName(fileName);
        return Task.FromResult(BuildPublicUrl(sanitised));
    }

    // -------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------

    /// <summary>
    /// Builds a public URL by combining the base URL with the file name,
    /// normalising directory separators.
    /// </summary>
    private string BuildPublicUrl(string fileName)
    {
        var normalised = fileName.Replace('\\', '/');
        return $"{_baseUrl}/{normalised}";
    }

    /// <summary>
    /// Prevents path-traversal attacks by stripping leading slashes,
    /// ".." segments, and rejecting absolute paths.
    /// </summary>
    private static string SanitiseFileName(string fileName)
    {
        // Reject rooted paths
        if (Path.IsPathRooted(fileName))
            fileName = Path.GetFileName(fileName);

        // Normalize separators
        fileName = fileName.Replace('\\', '/');

        // Resolve ".." segments safely
        var segments = fileName.Split('/', StringSplitOptions.RemoveEmptyEntries)
                               .Where(s => s != "..")
                               .ToArray();

        return string.Join('/', segments);
    }
}

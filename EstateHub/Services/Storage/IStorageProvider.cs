namespace EstateHub.Services.Storage;

/// <summary>
/// File upload result from storage operations
/// </summary>
public class StorageResult
{
    public bool Success { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? ErrorMessage { get; set; }
    public long FileSize { get; set; }

    public static StorageResult Ok(string url, string fileName, long size) =>
        new() { Success = true, FileUrl = url, FileName = fileName, FileSize = size };

    public static StorageResult Fail(string error) =>
        new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Unified storage interface supporting multiple providers
/// </summary>
public interface IStorageProvider
{
    /// <summary>Provider name for config/debugging</summary>
    string ProviderName { get; }

    /// <summary>Upload file from stream</summary>
    Task<StorageResult> UploadAsync(Stream stream, string fileName, string contentType, string subfolder = "");

    /// <summary>Upload file from byte array</summary>
    Task<StorageResult> UploadAsync(byte[] data, string fileName, string contentType, string subfolder = "");

    /// <summary>Delete file from storage</summary>
    Task<bool> DeleteAsync(string fileUrl);

    /// <summary>Check if file exists</summary>
    Task<bool> ExistsAsync(string fileUrl);

    /// <summary>Get public URL for a file</summary>
    string GetPublicUrl(string fileName, string subfolder = "");

    /// <summary>Download file as stream</summary>
    Task<Stream?> DownloadAsync(string fileUrl);

    /// <summary>List files in a subfolder</summary>
    Task<List<string>> ListFilesAsync(string subfolder = "", int maxResults = 100);
}

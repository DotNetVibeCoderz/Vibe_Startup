namespace LandLord.Services;

/// <summary>
/// Interface untuk penyimpanan file (multi-provider)
/// Supports: FileSystem, Azure Blob, AWS S3, MinIO
/// </summary>
public interface IStorageService
{
    /// <summary>Upload file ke storage, returns path/identifier</summary>
    Task<string> UploadAsync(string fileName, Stream fileStream, string contentType);

    /// <summary>Download file dari storage</summary>
    Task<Stream?> DownloadAsync(string filePath);

    /// <summary>Hapus file dari storage</summary>
    Task<bool> DeleteAsync(string filePath);

    /// <summary>Dapatkan URL publik untuk akses file</summary>
    Task<string> GetPublicUrlAsync(string filePath);

    /// <summary>Cek apakah file exists</summary>
    Task<bool> FileExistsAsync(string filePath);

    /// <summary>Dapatkan metadata/ informasi file</summary>
    Task<StorageFileInfo?> GetFileInfoAsync(string filePath);

    /// <summary>List semua file dalam folder/prefix</summary>
    Task<List<StorageFileInfo>> ListFilesAsync(string? prefix = null, int maxResults = 100);

    /// <summary>Dapatkan nama provider yang aktif</summary>
    string ProviderName { get; }

    /// <summary>Cek koneksi ke storage provider</summary>
    Task<bool> CheckConnectionAsync();
}

/// <summary>
/// Informasi file di storage
/// </summary>
public class StorageFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public string? PublicUrl { get; set; }
}

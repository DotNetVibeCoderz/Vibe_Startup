namespace SoccerWizard.Services.Storage;

/// <summary>
/// Abstraction untuk file storage. Mendukung multiple backend:
/// FileSystem, AWS S3, MinIO, Azure Blob Storage.
/// </summary>
public interface IStorageService
{
    /// <summary>Upload file dari stream ke storage.</summary>
    /// <returns>URL publik atau relatif ke file.</returns>
    Task<string> UploadAsync(string fileName, Stream stream, string contentType);

    /// <summary>Download file dari storage sebagai stream.</summary>
    Task<Stream?> DownloadAsync(string fileName);

    /// <summary>Hapus file dari storage.</summary>
    Task DeleteAsync(string fileName);

    /// <summary>Cek apakah file exists.</summary>
    Task<bool> ExistsAsync(string fileName);

    /// <summary>Dapatkan URL publik file.</summary>
    string GetPublicUrl(string fileName);

    /// <summary>Nama provider yang sedang aktif.</summary>
    string ProviderName { get; }
}

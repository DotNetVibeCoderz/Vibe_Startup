namespace FitnessCenter.Services.Storage;

/// <summary>
/// Interface abstraksi untuk semua provider penyimpanan
/// </summary>
public interface IStorageProvider
{
    /// <summary>Upload file stream dan kembalikan URL publik</summary>
    Task<string> UploadAsync(Stream fileStream, string fileName, string folder = "", string? contentType = null);

    /// <summary>Upload dari base64 string</summary>
    Task<string> UploadBase64Async(string base64, string fileName, string folder = "");

    /// <summary>Hapus file berdasarkan URL</summary>
    Task<bool> DeleteAsync(string fileUrl);

    /// <summary>Cek apakah file exists</summary>
    Task<bool> ExistsAsync(string fileUrl);

    /// <summary>Dapatkan public URL dari sebuah file key</summary>
    string GetPublicUrl(string fileKey, string folder);

    /// <summary>Nama provider</summary>
    string ProviderName { get; }
}

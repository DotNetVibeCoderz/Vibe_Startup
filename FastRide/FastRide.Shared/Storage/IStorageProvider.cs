namespace FastRide.Shared.Storage;

/// <summary>Unified storage abstraction for profile photos and document uploads.</summary>
public interface IStorageProvider
{
    /// <summary>Upload a file, returns public URL.</summary>
    Task<string> UploadAsync(string fileName, byte[] data, string contentType, CancellationToken ct = default);

    /// <summary>Download a file as byte array.</summary>
    Task<byte[]?> DownloadAsync(string fileName, CancellationToken ct = default);

    /// <summary>Delete a file. Returns true if deleted.</summary>
    Task<bool> DeleteAsync(string fileName, CancellationToken ct = default);

    /// <summary>Check if a file exists.</summary>
    Task<bool> ExistsAsync(string fileName, CancellationToken ct = default);

    /// <summary>Generate a unique file name for a user's photo.</summary>
    string GeneratePhotoFileName(Guid userId, string extension);
}

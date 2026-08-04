namespace FastRide.Shared.Storage;

/// <summary>Unified storage abstraction for profile photos and document uploads.</summary>
public interface IStorageProvider
{
    /// <summary>Human-readable provider name, surfaced on /api/health.</summary>
    string Name { get; }

    /// <summary>Upload a file, returns the public URL.</summary>
    Task<string> UploadAsync(string fileName, byte[] data, string contentType, CancellationToken ct = default);

    /// <summary>Download a file as a byte array.</summary>
    Task<byte[]?> DownloadAsync(string fileName, CancellationToken ct = default);

    /// <summary>Delete a file. Returns true if something was deleted.</summary>
    Task<bool> DeleteAsync(string fileName, CancellationToken ct = default);

    /// <summary>Check if a file exists.</summary>
    Task<bool> ExistsAsync(string fileName, CancellationToken ct = default);

    /// <summary>
    /// Turn a public URL produced by <see cref="UploadAsync"/> back into the storage key,
    /// so a stored URL can be deleted. Returns null when the URL did not come from this
    /// provider — data: avatar URIs, for instance, have nothing to delete.
    /// </summary>
    string? ResolveFileName(string url);

    /// <summary>Generate a unique file name for a user's photo.</summary>
    string GeneratePhotoFileName(Guid userId, string extension) => BuildFileName("photos", userId, extension);

    /// <summary>Generate a unique file name for a driver document.</summary>
    string GenerateDocumentFileName(Guid ownerId, string documentType, string extension) =>
        BuildFileName($"documents/{documentType.ToLowerInvariant()}", ownerId, extension);

    /// <summary>folder/owner_timestamp_random.ext — collision-safe and readable in a bucket listing.</summary>
    private static string BuildFileName(string folder, Guid ownerId, string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext)) ext = "bin";

        var owner = ownerId.ToString("N")[..12];
        var suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{folder}/{owner}_{DateTime.UtcNow:yyyyMMddHHmmss}_{suffix}.{ext}";
    }
}

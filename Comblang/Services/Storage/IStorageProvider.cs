namespace Comblang.Services.Storage;

/// <summary>
/// Abstraction for file storage operations across multiple backends
/// (FileSystem, MinIO, AWS S3, Azure Blob Storage).
/// </summary>
public interface IStorageProvider
{
    /// <summary>
    /// Uploads a file stream and returns the public URL to access it.
    /// </summary>
    /// <param name="fileName">Relative file name (may include subdirectories).</param>
    /// <param name="content">The file content stream.</param>
    /// <param name="contentType">MIME type of the content.</param>
    /// <returns>The public URL of the uploaded file.</returns>
    Task<string> UploadAsync(string fileName, Stream content, string contentType);

    /// <summary>
    /// Downloads a file as a stream, or null if not found.
    /// </summary>
    Task<Stream?> DownloadAsync(string fileName);

    /// <summary>
    /// Deletes a file. Returns true if the file existed and was deleted.
    /// </summary>
    Task<bool> DeleteAsync(string fileName);

    /// <summary>
    /// Returns the public URL for a stored file without validating existence.
    /// </summary>
    Task<string> GetPublicUrlAsync(string fileName);
}

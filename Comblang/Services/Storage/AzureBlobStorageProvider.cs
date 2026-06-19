using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Comblang.Services.Storage;

/// <summary>
/// Azure Blob Storage provider. Uses <c>Azure.Storage.Blobs</c> SDK to
/// interact with a blob container.
/// </summary>
public class AzureBlobStorageProvider : IStorageProvider
{
    private readonly BlobContainerClient _container;
    private readonly string _publicUrlPrefix;

    /// <summary>
    /// Creates an Azure Blob Storage provider.
    /// </summary>
    /// <param name="connectionString">Azure Storage connection string.</param>
    /// <param name="containerName">Blob container name.</param>
    /// <param name="publicUrlPrefix">
    /// Optional public URL prefix (e.g. CDN). Defaults to the blob's own URL.
    /// </param>
    public AzureBlobStorageProvider(
        string connectionString,
        string containerName,
        string? publicUrlPrefix = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString));
        if (string.IsNullOrWhiteSpace(containerName))
            throw new ArgumentNullException(nameof(containerName));

        var blobServiceClient = new BlobServiceClient(connectionString);
        _container = blobServiceClient.GetBlobContainerClient(containerName);

        // Ensure the container exists (idempotent)
        _container.CreateIfNotExists(PublicAccessType.Blob);

        _publicUrlPrefix = !string.IsNullOrWhiteSpace(publicUrlPrefix)
            ? publicUrlPrefix.TrimEnd('/')
            : _container.Uri.AbsoluteUri.TrimEnd('/');
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        var blobClient = _container.GetBlobClient(fileName);

        var headers = new BlobHttpHeaders { ContentType = contentType };

        // Reset stream position since it may have been read previously
        if (content.CanSeek)
            content.Position = 0;

        await blobClient.UploadAsync(content, headers);
        return $"{_publicUrlPrefix}/{fileName}";
    }

    /// <inheritdoc />
    public async Task<Stream?> DownloadAsync(string fileName)
    {
        var blobClient = _container.GetBlobClient(fileName);

        if (!await blobClient.ExistsAsync())
            return null;

        var response = await blobClient.DownloadStreamingAsync();
        return response.Value.Content;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string fileName)
    {
        var blobClient = _container.GetBlobClient(fileName);
        var result = await blobClient.DeleteIfExistsAsync();
        return result.Value;
    }

    /// <inheritdoc />
    public Task<string> GetPublicUrlAsync(string fileName)
    {
        var url = $"{_publicUrlPrefix}/{fileName}";
        return Task.FromResult(url);
    }
}

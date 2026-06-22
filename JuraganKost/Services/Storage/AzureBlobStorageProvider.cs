using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace JuraganKost.Services.Storage;

/// <summary>
/// Azure Blob Storage provider.
/// Stores files in Azure Blob containers with public access.
/// </summary>
public class AzureBlobStorageProvider : IStorageProvider
{
    private readonly BlobContainerClient _container;
    private readonly string _containerName;

    public AzureBlobStorageProvider(IConfiguration config)
    {
        var blobConfig = config.GetSection("StorageConfig:AzureBlob");
        var connString = blobConfig.GetValue<string>("ConnectionString") ?? "";
        _containerName = blobConfig.GetValue<string>("ContainerName") ?? "juragankost";

        if (string.IsNullOrEmpty(connString))
            throw new InvalidOperationException("AzureBlob ConnectionString is not configured.");

        var blobServiceClient = new BlobServiceClient(connString);
        _container = blobServiceClient.GetBlobContainerClient(_containerName);
        _container.CreateIfNotExists(PublicAccessType.Blob);
    }

    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        var ext = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid():N}{ext}";
        var blobClient = _container.GetBlobClient(uniqueName);

        var headers = new BlobHttpHeaders { ContentType = contentType };
        await blobClient.UploadAsync(content, headers);

        return blobClient.Uri.ToString();
    }

    public async Task<bool> DeleteAsync(string fileKey)
    {
        try
        {
            var uri = new Uri(fileKey);
            var blobName = uri.Segments.Last();
            var blobClient = _container.GetBlobClient(blobName);
            return await blobClient.DeleteIfExistsAsync();
        }
        catch { return false; }
    }

    public async Task<Stream?> DownloadAsync(string fileKey)
    {
        try
        {
            var uri = new Uri(fileKey);
            var blobName = uri.Segments.Last();
            var blobClient = _container.GetBlobClient(blobName);
            if (!await blobClient.ExistsAsync()) return null;

            var ms = new MemoryStream();
            await blobClient.DownloadToAsync(ms);
            ms.Position = 0;
            return ms;
        }
        catch { return null; }
    }

    public string GetPublicUrl(string fileKey) => fileKey;

    public async Task<bool> ExistsAsync(string fileKey)
    {
        try
        {
            var uri = new Uri(fileKey);
            var blobName = uri.Segments.Last();
            return await _container.GetBlobClient(blobName).ExistsAsync();
        }
        catch { return false; }
    }

    public async Task<List<string>> ListAsync(string? prefix = null)
    {
        var results = new List<string>();
        await foreach (var blob in _container.GetBlobsAsync(prefix: prefix))
            results.Add(_container.GetBlobClient(blob.Name).Uri.ToString());
        return results;
    }
}

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace EstateHub.Services.Storage;

public class AzureBlobStorageProvider : IStorageProvider
{
    private readonly BlobContainerClient _container;
    public string ProviderName => "AzureBlob";

    public AzureBlobStorageProvider(IConfiguration config)
    {
        var connStr = config.GetValue<string>("Storage:AzureBlob:ConnectionString") ?? throw new InvalidOperationException("AzureBlob ConnectionString required");
        var containerName = config.GetValue<string>("Storage:AzureBlob:ContainerName") ?? "estatehub";
        _container = new BlobServiceClient(connStr).GetBlobContainerClient(containerName);
        _container.CreateIfNotExists(PublicAccessType.Blob);
    }

    public async Task<StorageResult> UploadAsync(Stream stream, string fileName, string contentType, string subfolder = "")
    {
        try
        {
            var blobName = string.IsNullOrEmpty(subfolder) ? $"{Guid.NewGuid():N}_{fileName}" : $"{subfolder}/{Guid.NewGuid():N}_{fileName}";
            var blobClient = _container.GetBlobClient(blobName);
            await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } });
            return StorageResult.Ok(blobClient.Uri.ToString(), Path.GetFileName(fileName), stream.Length);
        }
        catch (Exception ex) { return StorageResult.Fail(ex.Message); }
    }

    public async Task<StorageResult> UploadAsync(byte[] data, string fileName, string contentType, string subfolder = "")
    {
        using var stream = new MemoryStream(data);
        return await UploadAsync(stream, fileName, contentType, subfolder);
    }

    public async Task<bool> DeleteAsync(string fileUrl)
    {
        try { return await _container.GetBlobClient(new Uri(fileUrl).Segments.Last()).DeleteIfExistsAsync(); }
        catch { return false; }
    }

    public async Task<bool> ExistsAsync(string fileUrl)
    {
        try { return await _container.GetBlobClient(new Uri(fileUrl).Segments.Last()).ExistsAsync(); }
        catch { return false; }
    }

    public string GetPublicUrl(string fileName, string subfolder = "")
    {
        var blobName = string.IsNullOrEmpty(subfolder) ? fileName : $"{subfolder}/{fileName}";
        return _container.GetBlobClient(blobName).Uri.ToString();
    }

    public async Task<Stream?> DownloadAsync(string fileUrl)
    {
        try
        {
            var blobClient = _container.GetBlobClient(new Uri(fileUrl).Segments.Last());
            if (!await blobClient.ExistsAsync()) return null;
            var stream = new MemoryStream(); await blobClient.DownloadToAsync(stream); stream.Position = 0; return stream;
        }
        catch { return null; }
    }

    public async Task<List<string>> ListFilesAsync(string subfolder = "", int maxResults = 100)
    {
        var files = new List<string>();
        var prefix = string.IsNullOrEmpty(subfolder) ? "" : $"{subfolder}/";
        await foreach (var blob in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, CancellationToken.None).Take(maxResults))
            files.Add(_container.GetBlobClient(blob.Name).Uri.ToString());
        return files;
    }
}

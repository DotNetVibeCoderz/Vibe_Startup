using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace SoccerWizard.Services.Storage;

/// <summary>
/// Storage backend: Azure Blob Storage.
/// </summary>
public class AzureBlobStorageService : IStorageService
{
    private readonly BlobContainerClient _container;
    public string ProviderName => "AzureBlob";

    public AzureBlobStorageService(IConfiguration config)
    {
        var connectionString = config["Storage:AzureBlob:ConnectionString"] ?? "";
        var containerName = config["Storage:AzureBlob:ContainerName"] ?? "soccerwizard-uploads";

        var blobServiceClient = new BlobServiceClient(connectionString);
        _container = blobServiceClient.GetBlobContainerClient(containerName);

        // Ensure container exists
        Task.Run(async () => await _container.CreateIfNotExistsAsync()).Wait();
    }

    public async Task<string> UploadAsync(string fileName, Stream stream, string contentType)
    {
        var blobName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var blobClient = _container.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders { ContentType = contentType };
        await blobClient.UploadAsync(stream, headers);

        return blobClient.Uri.ToString();
    }

    public async Task<Stream?> DownloadAsync(string fileName)
    {
        try
        {
            var blobClient = _container.GetBlobClient(fileName);
            var ms = new MemoryStream();
            await blobClient.DownloadToAsync(ms);
            ms.Position = 0;
            return ms;
        }
        catch { return null; }
    }

    public async Task DeleteAsync(string fileName)
    {
        var blobClient = _container.GetBlobClient(fileName);
        await blobClient.DeleteIfExistsAsync();
    }

    public async Task<bool> ExistsAsync(string fileName)
    {
        var blobClient = _container.GetBlobClient(fileName);
        return await blobClient.ExistsAsync();
    }

    public string GetPublicUrl(string fileName)
    {
        var blobClient = _container.GetBlobClient(fileName);
        return blobClient.Uri.ToString();
    }
}

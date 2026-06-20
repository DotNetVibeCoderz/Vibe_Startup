using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace PDA.Services.Storage;

/// <summary>
/// Azure Blob Storage implementation.
/// Stores files in Azure Blob containers with SAS token access.
/// </summary>
public class AzureBlobStorageService : IStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(IConfiguration configuration, ILogger<AzureBlobStorageService> logger)
    {
        _logger = logger;
        var connectionString = configuration["Storage:AzureBlob:ConnectionString"] 
            ?? throw new InvalidOperationException("AzureBlob:ConnectionString not configured");
        _containerName = configuration["Storage:AzureBlob:ContainerName"] ?? "pda-uploads";
        
        _blobServiceClient = new BlobServiceClient(connectionString);
        
        // Ensure container exists
        var container = _blobServiceClient.GetBlobContainerClient(_containerName);
        container.CreateIfNotExists(PublicAccessType.None);
        _logger.LogInformation("Azure Blob Storage initialized: container={Container}", _containerName);
    }

    /// <summary>
    /// Upload a file to Azure Blob Storage
    /// </summary>
    public async Task<string> UploadAsync(string fileName, Stream content, string contentType)
    {
        try
        {
            var uniqueName = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{Path.GetExtension(fileName)}";
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(uniqueName);

            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            };

            await blobClient.UploadAsync(content, options);
            _logger.LogInformation("File uploaded to Azure Blob: {FileName} -> {BlobName}", fileName, uniqueName);

            return uniqueName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload to Azure Blob: {FileName}", fileName);
            throw;
        }
    }

    /// <summary>
    /// Download a file from Azure Blob Storage
    /// </summary>
    public async Task<Stream?> DownloadAsync(string filePath)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(filePath);

            if (!await blobClient.ExistsAsync())
                return null;

            var stream = new MemoryStream();
            await blobClient.DownloadToAsync(stream);
            stream.Position = 0;
            return stream;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download from Azure Blob: {Path}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Delete a file from Azure Blob Storage
    /// </summary>
    public async Task<bool> DeleteAsync(string filePath)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(filePath);
            return await blobClient.DeleteIfExistsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete from Azure Blob: {Path}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Get a public URL with SAS token for temporary access
    /// </summary>
    public async Task<string> GetPublicUrlAsync(string filePath)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(filePath);

            if (!await blobClient.ExistsAsync())
                return string.Empty;

            // Generate SAS token valid for 24 hours
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = _containerName,
                BlobName = filePath,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(24)
            };
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            return sasUri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SAS URL: {Path}", filePath);
            return string.Empty;
        }
    }

    /// <summary>
    /// Check if a file exists in Azure Blob
    /// </summary>
    public async Task<bool> ExistsAsync(string filePath)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(filePath);
            return await blobClient.ExistsAsync();
        }
        catch
        {
            return false;
        }
    }
}

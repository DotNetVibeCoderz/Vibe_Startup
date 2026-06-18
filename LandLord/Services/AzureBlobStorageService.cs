using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace LandLord.Services;

/// <summary>
/// Implementasi penyimpanan file menggunakan Azure Blob Storage
/// </summary>
public class AzureBlobStorageService : IStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly long _maxFileSize;
    private readonly string[] _allowedExtensions;
    private readonly string? _cdnEndpoint;

    public string ProviderName => "AzureBlob";

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration.GetValue<string>("StorageProvider:AzureBlob:ConnectionString")
            ?? throw new InvalidOperationException("Azure Blob Storage connection string is required.");

        _containerName = configuration.GetValue<string>("StorageProvider:AzureBlob:ContainerName") ?? "landlord-documents";
        _cdnEndpoint = configuration.GetValue<string>("StorageProvider:AzureBlob:CdnEndpoint");
        _maxFileSize = configuration.GetValue<long>("StorageProvider:MaxFileSizeMB", 50) * 1024 * 1024;

        var extensions = configuration.GetSection("StorageProvider:AllowedExtensions").Get<string[]>();
        _allowedExtensions = extensions ?? new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".mp4", ".doc", ".docx", ".xls", ".xlsx" };

        _blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        containerClient.CreateIfNotExists(PublicAccessType.Blob);
    }

    public async Task<string> UploadAsync(string fileName, Stream fileStream, string contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(extension))
            throw new InvalidOperationException($"Ekstensi file '{extension}' tidak diizinkan.");

        if (fileStream.Length > _maxFileSize)
            throw new InvalidOperationException($"Ukuran file melebihi batas maksimum {_maxFileSize / 1024.0 / 1024.0:F0} MB.");

        var dateFolder = DateTime.UtcNow.ToString("yyyy/MM/dd");
        var uniqueName = $"{dateFolder}/{Guid.NewGuid():N}{extension}";

        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(uniqueName);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType, CacheControl = "public, max-age=86400" },
            Metadata = new Dictionary<string, string> { ["OriginalFileName"] = fileName, ["UploadedAt"] = DateTime.UtcNow.ToString("O") }
        };

        await blobClient.UploadAsync(fileStream, uploadOptions);
        return uniqueName;
    }

    public async Task<Stream?> DownloadAsync(string filePath)
    {
        try
        {
            var blobClient = _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient(filePath);
            if (!await blobClient.ExistsAsync()) return null;
            var stream = new MemoryStream();
            await blobClient.DownloadToAsync(stream);
            stream.Position = 0;
            return stream;
        }
        catch (RequestFailedException) { return null; }
    }

    public async Task<bool> DeleteAsync(string filePath)
    {
        try { return await _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient(filePath).DeleteIfExistsAsync(); }
        catch (RequestFailedException) { return false; }
    }

    public Task<string> GetPublicUrlAsync(string filePath)
    {
        var blobClient = _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient(filePath);
        if (!string.IsNullOrEmpty(_cdnEndpoint))
            return Task.FromResult($"{_cdnEndpoint.TrimEnd('/')}/{_containerName}/{filePath}");
        return Task.FromResult(blobClient.Uri.ToString());
    }

    public async Task<bool> FileExistsAsync(string filePath)
    {
        try { return await _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient(filePath).ExistsAsync(); }
        catch { return false; }
    }

    public async Task<StorageFileInfo?> GetFileInfoAsync(string filePath)
    {
        try
        {
            var blobClient = _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient(filePath);
            if (!await blobClient.ExistsAsync()) return null;
            var properties = await blobClient.GetPropertiesAsync();
            var metadata = properties.Value.Metadata;
            return new StorageFileInfo
            {
                FileName = metadata.TryGetValue("OriginalFileName", out var originalName) ? originalName : Path.GetFileName(filePath),
                FilePath = filePath, FileSize = properties.Value.ContentLength,
                ContentType = properties.Value.ContentType,
                LastModified = properties.Value.LastModified.UtcDateTime,
                PublicUrl = await GetPublicUrlAsync(filePath)
            };
        }
        catch (RequestFailedException) { return null; }
    }

    public async Task<List<StorageFileInfo>> ListFilesAsync(string? prefix = null, int maxResults = 100)
    {
        var files = new List<StorageFileInfo>();
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var asyncPageable = containerClient.GetBlobsAsync(
                traits: BlobTraits.Metadata,
                states: BlobStates.None,
                prefix: prefix,
                cancellationToken: CancellationToken.None);

            await foreach (var blobItem in asyncPageable.Take(maxResults))
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                files.Add(new StorageFileInfo
                {
                    FileName = blobItem.Metadata.TryGetValue("OriginalFileName", out var name) ? name : Path.GetFileName(blobItem.Name),
                    FilePath = blobItem.Name,
                    FileSize = blobItem.Properties.ContentLength ?? 0,
                    ContentType = blobItem.Properties.ContentType ?? "application/octet-stream",
                    LastModified = blobItem.Properties.LastModified?.UtcDateTime ?? DateTime.MinValue,
                    PublicUrl = !string.IsNullOrEmpty(_cdnEndpoint) ? $"{_cdnEndpoint.TrimEnd('/')}/{_containerName}/{blobItem.Name}" : blobClient.Uri.ToString()
                });
            }
        }
        catch (RequestFailedException) { }
        return files;
    }

    public async Task<bool> CheckConnectionAsync()
    {
        try { await _blobServiceClient.GetBlobContainerClient(_containerName).GetPropertiesAsync(); return true; }
        catch (RequestFailedException) { return false; }
    }

    public string GenerateSasUrl(string filePath, TimeSpan expiry)
    {
        var blobClient = _blobServiceClient.GetBlobContainerClient(_containerName).GetBlobClient(filePath);
        var sasBuilder = new BlobSasBuilder { BlobContainerName = _containerName, BlobName = filePath, Resource = "b", ExpiresOn = DateTimeOffset.UtcNow.Add(expiry) };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);
        return blobClient.GenerateSasUri(sasBuilder).ToString();
    }
}

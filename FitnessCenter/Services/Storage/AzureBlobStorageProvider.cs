using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace FitnessCenter.Services.Storage;

/// <summary>
/// Azure Blob Storage provider.
/// Menyimpan file ke Azure Blob Container.
/// </summary>
public class AzureBlobStorageProvider : IStorageProvider, IDisposable
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;
    private readonly string _cdnEndpoint;
    private bool _disposed;

    public string ProviderName => "AzureBlob";

    public AzureBlobStorageProvider(IConfiguration config)
    {
        var connectionString = config.GetValue<string>("Storage:AzureBlob:ConnectionString")
            ?? throw new InvalidOperationException("AzureBlob ConnectionString is not configured.");

        _containerName = config.GetValue<string>("Storage:AzureBlob:ContainerName") ?? "fitnesscenter";
        _cdnEndpoint = config.GetValue<string>("Storage:AzureBlob:CdnEndpoint") ?? string.Empty;

        _blobServiceClient = new BlobServiceClient(connectionString);

        // Pastikan container exists
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        containerClient.CreateIfNotExists(PublicAccessType.Blob);
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string folder = "", string? contentType = null)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);

        // Build blob path: folder/unique_filename
        var extension = Path.GetExtension(fileName);
        var uniqueName = $"{Guid.NewGuid():N}_{SanitizeFileName(Path.GetFileNameWithoutExtension(fileName))}{extension}";
        var blobPath = string.IsNullOrEmpty(folder) ? uniqueName : $"{folder}/{uniqueName}";

        var blobClient = containerClient.GetBlobClient(blobPath);

        // Upload dengan content type jika disediakan
        var uploadOptions = new BlobUploadOptions();
        if (!string.IsNullOrEmpty(contentType))
        {
            uploadOptions.HttpHeaders = new BlobHttpHeaders { ContentType = contentType };
        }
        else
        {
            // Deteksi content type dari ekstensi
            var detectedContentType = GetContentType(fileName);
            if (!string.IsNullOrEmpty(detectedContentType))
            {
                uploadOptions.HttpHeaders = new BlobHttpHeaders { ContentType = detectedContentType };
            }
        }

        // Reset stream position
        if (fileStream.CanSeek) fileStream.Position = 0;

        await blobClient.UploadAsync(fileStream, uploadOptions);

        // Return URL
        return GetPublicUrl(uniqueName, folder);
    }

    public async Task<string> UploadBase64Async(string base64, string fileName, string folder = "")
    {
        var cleanBase64 = base64.Contains(',') ? base64[(base64.IndexOf(',') + 1)..] : base64;
        var bytes = Convert.FromBase64String(cleanBase64);
        using var ms = new MemoryStream(bytes);

        // Deteksi content type dari base64 prefix
        string? contentType = null;
        if (base64.Contains("data:"))
        {
            var prefix = base64[..base64.IndexOf(';')];
            contentType = prefix.Replace("data:", "");
        }

        return await UploadAsync(ms, fileName, folder, contentType);
    }

    public async Task<bool> DeleteAsync(string fileUrl)
    {
        try
        {
            var blobPath = ExtractBlobPath(fileUrl);
            if (string.IsNullOrEmpty(blobPath)) return false;

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);
            return await blobClient.DeleteIfExistsAsync();
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string fileUrl)
    {
        try
        {
            var blobPath = ExtractBlobPath(fileUrl);
            if (string.IsNullOrEmpty(blobPath)) return false;

            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            var blobClient = containerClient.GetBlobClient(blobPath);
            return await blobClient.ExistsAsync();
        }
        catch
        {
            return false;
        }
    }

    public string GetPublicUrl(string fileKey, string folder)
    {
        var blobPath = string.IsNullOrEmpty(folder) ? fileKey : $"{folder}/{fileKey}";

        // Gunakan CDN endpoint jika tersedia
        if (!string.IsNullOrEmpty(_cdnEndpoint))
        {
            return $"{_cdnEndpoint.TrimEnd('/')}/{_containerName}/{blobPath}";
        }

        // Default Azure Blob URL
        return $"{_blobServiceClient.Uri.AbsoluteUri.TrimEnd('/')}/{_containerName}/{blobPath}";
    }

    /// <summary>Ekstrak blob path dari full URL</summary>
    private string ExtractBlobPath(string url)
    {
        try
        {
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // Cari segment setelah container name
            var containerIndex = Array.FindIndex(segments, s =>
                s.Equals(_containerName, StringComparison.OrdinalIgnoreCase));

            if (containerIndex >= 0 && containerIndex < segments.Length - 1)
            {
                return string.Join("/", segments[(containerIndex + 1)..]);
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        // Blob storage lebih toleran, tapi tetap sanitasi karakter khusus
        return fileName.Replace("\\", "/")
                       .Replace("#", "")
                       .Replace("?", "")
                       .Replace("&", "");
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".mp4" => "video/mp4",
            ".mp3" => "audio/mpeg",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            // BlobServiceClient doesn't need explicit disposal in newer SDK versions
        }
    }
}

using Azure.Storage.Blobs;
using Amazon.S3;
using Amazon.S3.Model;
using Minio;
using Minio.DataModel.Args;
using SmartDrive.Models.Enums;

namespace SmartDrive.Services;

public interface IStorageService
{
    Task<string> UploadAsync(Stream stream, string fileName, string subFolder = "");
    Task<Stream?> DownloadAsync(string filePath);
    Task<bool> DeleteAsync(string filePath);
    Task<bool> ExistsAsync(string filePath);
    string GetPublicUrl(string filePath);
}

public class StorageService : IStorageService
{
    private readonly IConfiguration _cfg;
    private readonly IWebHostEnvironment _env;
    private readonly StorageProvider _provider;
    private readonly string _basePath;
    private BlobServiceClient? _blobClient;
    private IAmazonS3? _s3Client;
    private IMinioClient? _minioClient;
    private string _containerName = "smartdrive";
    private string _bucketName = "smartdrive";

    public StorageService(IConfiguration cfg, IWebHostEnvironment env)
    {
        _cfg = cfg; _env = env;
        _provider = Enum.TryParse<StorageProvider>(_cfg.GetValue("Storage:Provider", "FileSystem"), out var p) ? p : StorageProvider.FileSystem;
        _basePath = _cfg.GetValue("Storage:BasePath", "uploads")!;
        _containerName = _cfg.GetValue("Storage:AzureBlob:ContainerName", "smartdrive")!;
        _bucketName = _cfg.GetValue("Storage:S3:BucketName", "smartdrive")!;
        if (string.IsNullOrEmpty(_bucketName)) _bucketName = _cfg.GetValue("Storage:MinIO:BucketName", "smartdrive")!;
        InitProvider();
    }

    private void InitProvider()
    {
        switch (_provider)
        {
            case StorageProvider.AzureBlob:
                var cs = _cfg.GetValue("Storage:AzureBlob:ConnectionString", "");
                if (!string.IsNullOrEmpty(cs)) { _blobClient = new BlobServiceClient(cs); try { _blobClient.GetBlobContainerClient(_containerName).CreateIfNotExists(); } catch { } }
                break;
            case StorageProvider.S3:
                var ak = _cfg.GetValue("Storage:S3:AccessKey", "");
                var sk = _cfg.GetValue("Storage:S3:SecretKey", "");
                var region = _cfg.GetValue("Storage:S3:Region", "ap-southeast-1")!;
                var svcUrl = _cfg.GetValue("Storage:S3:ServiceUrl", "");
                var cfg3 = new AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region), ForcePathStyle = true };
                if (!string.IsNullOrEmpty(svcUrl)) cfg3.ServiceURL = svcUrl;
                if (!string.IsNullOrEmpty(ak) && !string.IsNullOrEmpty(sk)) { _s3Client = new AmazonS3Client(ak, sk, cfg3); try { _s3Client.PutBucketAsync(_bucketName).Wait(); } catch { } }
                break;
            case StorageProvider.MinIO:
                var ep = _cfg.GetValue("Storage:MinIO:Endpoint", "localhost:9000")!;
                var ma = _cfg.GetValue("Storage:MinIO:AccessKey", "minioadmin")!;
                var ms = _cfg.GetValue("Storage:MinIO:SecretKey", "minioadmin")!;
                var ssl = _cfg.GetValue("Storage:MinIO:UseSsl", false);
                _minioClient = new MinioClient().WithEndpoint(ep).WithCredentials(ma, ms).WithSSL(ssl).Build();
                try { _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucketName)).Wait(); } catch { }
                break;
        }
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string subFolder = "")
    {
        return _provider switch
        {
            StorageProvider.AzureBlob => await UploadToAzureAsync(stream, fileName, subFolder),
            StorageProvider.S3 => await UploadToS3Async(stream, fileName, subFolder),
            StorageProvider.MinIO => await UploadToMinIOAsync(stream, fileName, subFolder),
            _ => await UploadToFsAsync(stream, fileName, subFolder)
        };
    }

    public async Task<Stream?> DownloadAsync(string filePath)
    {
        return _provider switch
        {
            StorageProvider.AzureBlob => await DLAzureAsync(filePath),
            StorageProvider.S3 => await DLS3Async(filePath),
            StorageProvider.MinIO => await DLMinIOAsync(filePath),
            _ => DLFs(filePath)
        };
    }

    public async Task<bool> DeleteAsync(string filePath)
    {
        try
        {
            switch (_provider)
            {
                case StorageProvider.AzureBlob:
                    var c = _blobClient!.GetBlobContainerClient(_containerName);
                    return await c.GetBlobClient(GetKey(filePath)).DeleteIfExistsAsync();
                case StorageProvider.S3:
                    await _s3Client!.DeleteObjectAsync(_bucketName, GetKey(filePath));
                    return true;
                case StorageProvider.MinIO:
                    await _minioClient!.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_bucketName).WithObject(GetKey(filePath)));
                    return true;
                default:
                    var fp = Path.Combine(_env.WebRootPath, filePath.TrimStart('/'));
                    if (File.Exists(fp)) { File.Delete(fp); return true; }
                    return false;
            }
        }
        catch { return false; }
    }

    public async Task<bool> ExistsAsync(string filePath)
    {
        try
        {
            switch (_provider)
            {
                case StorageProvider.AzureBlob:
                    return await _blobClient!.GetBlobContainerClient(_containerName).GetBlobClient(GetKey(filePath)).ExistsAsync();
                case StorageProvider.S3:
                    var meta = await _s3Client!.GetObjectMetadataAsync(_bucketName, GetKey(filePath));
                    return meta != null;
                case StorageProvider.MinIO:
                    var stat = await _minioClient!.StatObjectAsync(new StatObjectArgs().WithBucket(_bucketName).WithObject(GetKey(filePath)));
                    return stat != null;
                default:
                    return File.Exists(Path.Combine(_env.WebRootPath, filePath.TrimStart('/')));
            }
        }
        catch { return false; }
    }

    public string GetPublicUrl(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return "";
        return _provider switch
        {
            StorageProvider.AzureBlob => $"{_blobClient!.Uri}{_containerName}/{GetKey(filePath)}",
            StorageProvider.S3 => $"https://{_bucketName}.s3.amazonaws.com/{GetKey(filePath)}",
            StorageProvider.MinIO => $"http://{_cfg.GetValue("Storage:MinIO:Endpoint","localhost:9000")!}/{_bucketName}/{GetKey(filePath)}",
            _ => (filePath.StartsWith("/") ? filePath : "/" + filePath)
        };
    }

    // FileSystem
    private async Task<string> UploadToFsAsync(Stream stream, string fileName, string subFolder)
    {
        var dir = Path.Combine(_env.WebRootPath, _basePath, subFolder); Directory.CreateDirectory(dir);
        var fn = $"{Guid.NewGuid()}_{fileName}"; var fp = Path.Combine(dir, fn);
        using var fs = new FileStream(fp, FileMode.Create); await stream.CopyToAsync(fs);
        return $"/{_basePath}/{subFolder}/{fn}".Replace("//", "/");
    }
    private Stream? DLFs(string filePath) { var fp = Path.Combine(_env.WebRootPath, filePath.TrimStart('/')); return File.Exists(fp) ? File.OpenRead(fp) : null; }

    // Azure
    private async Task<string> UploadToAzureAsync(Stream stream, string fileName, string subFolder)
    {
        if (_blobClient == null) { var cs = _cfg.GetValue("Storage:AzureBlob:ConnectionString", "")!; if (string.IsNullOrEmpty(cs)) throw new InvalidOperationException("Azure not configured"); _blobClient = new BlobServiceClient(cs); }
        var c = _blobClient.GetBlobContainerClient(_containerName); await c.CreateIfNotExistsAsync();
        var key = string.IsNullOrEmpty(subFolder) ? $"{Guid.NewGuid()}_{fileName}" : $"{subFolder}/{Guid.NewGuid()}_{fileName}";
        await c.GetBlobClient(key).UploadAsync(stream, true);
        return key;
    }
    private async Task<Stream?> DLAzureAsync(string filePath) { if (_blobClient == null) return null; var c = _blobClient.GetBlobContainerClient(_containerName); var b = c.GetBlobClient(GetKey(filePath)); if (!await b.ExistsAsync()) return null; var ms = new MemoryStream(); await b.DownloadToAsync(ms); ms.Position = 0; return ms; }

    // S3
    private async Task<string> UploadToS3Async(Stream stream, string fileName, string subFolder)
    {
        if (_s3Client == null) { var ak = _cfg.GetValue("Storage:S3:AccessKey", "")!; if (string.IsNullOrEmpty(ak)) throw new InvalidOperationException("S3 not configured"); var sk = _cfg.GetValue("Storage:S3:SecretKey", "")!; var cfg3 = new AmazonS3Config { RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_cfg.GetValue("Storage:S3:Region", "ap-southeast-1")!), ForcePathStyle = true }; var svcUrl = _cfg.GetValue("Storage:S3:ServiceUrl", ""); if (!string.IsNullOrEmpty(svcUrl)) cfg3.ServiceURL = svcUrl; _s3Client = new AmazonS3Client(ak, sk, cfg3); }
        var key = string.IsNullOrEmpty(subFolder) ? $"{Guid.NewGuid()}_{fileName}" : $"{subFolder}/{Guid.NewGuid()}_{fileName}";
        await _s3Client.PutObjectAsync(new PutObjectRequest { BucketName = _bucketName, Key = key, InputStream = stream, AutoCloseStream = false });
        return key;
    }
    private async Task<Stream?> DLS3Async(string filePath) { if (_s3Client == null) return null; try { var r = await _s3Client.GetObjectAsync(_bucketName, GetKey(filePath)); var ms = new MemoryStream(); await r.ResponseStream.CopyToAsync(ms); ms.Position = 0; return ms; } catch { return null; } }

    // MinIO
    private async Task<string> UploadToMinIOAsync(Stream stream, string fileName, string subFolder)
    {
        if (_minioClient == null) { var ep = _cfg.GetValue("Storage:MinIO:Endpoint", "localhost:9000")!; _minioClient = new MinioClient().WithEndpoint(ep).WithCredentials(_cfg.GetValue("Storage:MinIO:AccessKey","minioadmin")!, _cfg.GetValue("Storage:MinIO:SecretKey","minioadmin")!).WithSSL(_cfg.GetValue("Storage:MinIO:UseSsl",false)).Build(); }
        var key = string.IsNullOrEmpty(subFolder) ? $"{Guid.NewGuid()}_{fileName}" : $"{subFolder}/{Guid.NewGuid()}_{fileName}";
        await _minioClient.PutObjectAsync(new PutObjectArgs().WithBucket(_bucketName).WithObject(key).WithStreamData(stream).WithObjectSize(stream.Length));
        return key;
    }
    private async Task<Stream?> DLMinIOAsync(string filePath) { if (_minioClient == null) return null; try { var ms = new MemoryStream(); await _minioClient.GetObjectAsync(new GetObjectArgs().WithBucket(_bucketName).WithObject(GetKey(filePath)).WithCallbackStream(s => s.CopyTo(ms))); ms.Position = 0; return ms; } catch { return null; } }

    private string GetKey(string fp) => fp.Contains('/') ? fp.Split('/').Last() : fp;
}

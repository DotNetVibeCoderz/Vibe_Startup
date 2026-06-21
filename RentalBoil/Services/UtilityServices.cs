using System.Globalization;
using System.Text;
using Azure.Storage.Blobs;
using Amazon.S3;
using Amazon.S3.Model;
using Minio;
using Minio.DataModel.Args;
using CsvHelper;
using CsvHelper.Configuration;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RentalBoil.Data;

namespace RentalBoil.Services;

public class ExportService
{
    public ExportService() { }

    public async Task<byte[]> ExportToCsvAsync<T>(IEnumerable<T> records)
    {
        using var ms = new MemoryStream();
        using var sw = new StreamWriter(ms, Encoding.UTF8);
        using var csv = new CsvWriter(sw, new CsvConfiguration(CultureInfo.InvariantCulture));
        await csv.WriteRecordsAsync(records);
        await sw.FlushAsync();
        return ms.ToArray();
    }

    public byte[] ExportToExcel<T>(IEnumerable<T> records, string sheetName = "Data")
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(sheetName);
        var props = typeof(T).GetProperties().Where(p => p.CanRead && !p.PropertyType.IsGenericType).ToArray();
        for (int i = 0; i < props.Length; i++) { var c = ws.Cell(1, i + 1); c.Value = props[i].Name; c.Style.Font.Bold = true; c.Style.Fill.BackgroundColor = XLColor.FromHtml("#4A90D9"); c.Style.Font.FontColor = XLColor.White; }
        int row = 2;
        foreach (var r in records) { for (int col = 0; col < props.Length; col++) ws.Cell(row, col + 1).Value = props[col].GetValue(r)?.ToString() ?? ""; row++; }
        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream(); wb.SaveAs(stream); return stream.ToArray();
    }
}

/// <summary>
/// Storage Service — FileSystem (default), Azure Blob, AWS S3, MinIO.
/// Provider dipilih dari appsettings.json → Storage:Provider.
/// </summary>
public class StorageService
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public StorageService(IConfiguration config, IWebHostEnvironment env)
    { _config = config; _env = env; }

    private string Provider => _config.GetValue<string>("Storage:Provider") ?? "FileSystem";
    private string BasePath => _config.GetValue<string>("Storage:BasePath") ?? "wwwroot/uploads";
    private string BaseUrl => _config.GetValue<string>("Storage:BaseUrl") ?? "/uploads";

    /// <summary>
    /// Upload file ke storage provider yang dikonfigurasi.
    /// </summary>
    public async Task<string> UploadFileAsync(IFormFile file, string folder = "general")
    {
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var relativePath = $"{folder}/{fileName}";

        return Provider switch
        {
            "AzureBlob" => await UploadToAzureBlobAsync(file, relativePath),
            "S3" => await UploadToS3Async(file, relativePath),
            "MinIO" => await UploadToMinIOAsync(file, relativePath),
            _ => await UploadToFileSystemAsync(file, relativePath)
        };
    }

    /// <summary>
    /// Hapus file dari storage provider yang dikonfigurasi.
    /// </summary>
    public async Task<bool> DeleteFileAsync(string fileUrl)
    {
        // Extract relative path from URL
        var relativePath = fileUrl.StartsWith(BaseUrl) ? fileUrl[BaseUrl.Length..].TrimStart('/') : fileUrl.TrimStart('/');

        return Provider switch
        {
            "AzureBlob" => await DeleteFromAzureBlobAsync(relativePath),
            "S3" => await DeleteFromS3Async(relativePath),
            "MinIO" => await DeleteFromMinIOAsync(relativePath),
            _ => await DeleteFromFileSystemAsync(relativePath)
        };
    }

    // ═══════════════════════ FILE SYSTEM ═══════════════════════

    private async Task<string> UploadToFileSystemAsync(IFormFile file, string relativePath)
    {
        var fullPath = Path.Combine(_env.ContentRootPath, BasePath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);
        return $"{BaseUrl}/{relativePath}";
    }

    private Task<bool> DeleteFromFileSystemAsync(string relativePath)
    {
        var fullPath = Path.Combine(_env.ContentRootPath, BasePath, relativePath);
        if (File.Exists(fullPath)) { File.Delete(fullPath); return Task.FromResult(true); }
        return Task.FromResult(false);
    }

    // ═══════════════════════ AZURE BLOB ═══════════════════════

    private async Task<string> UploadToAzureBlobAsync(IFormFile file, string relativePath)
    {
        var connStr = _config.GetValue<string>("Storage:AzureBlob:ConnectionString");
        var container = _config.GetValue<string>("Storage:AzureBlob:ContainerName") ?? "rentalboil";

        if (string.IsNullOrWhiteSpace(connStr))
            throw new InvalidOperationException("AzureBlob ConnectionString not configured in appsettings.json → Storage:AzureBlob:ConnectionString");

        var blobClient = new BlobContainerClient(connStr, container);
        await blobClient.CreateIfNotExistsAsync();
        var blob = blobClient.GetBlobClient(relativePath);

        using var stream = file.OpenReadStream();
        await blob.UploadAsync(stream, overwrite: true);

        return blob.Uri.ToString();
    }

    private async Task<bool> DeleteFromAzureBlobAsync(string relativePath)
    {
        var connStr = _config.GetValue<string>("Storage:AzureBlob:ConnectionString");
        var container = _config.GetValue<string>("Storage:AzureBlob:ContainerName") ?? "rentalboil";
        if (string.IsNullOrWhiteSpace(connStr)) return false;

        var blobClient = new BlobContainerClient(connStr, container);
        var blob = blobClient.GetBlobClient(relativePath);
        return await blob.DeleteIfExistsAsync();
    }

    // ═══════════════════════ AWS S3 ═══════════════════════

    private async Task<string> UploadToS3Async(IFormFile file, string relativePath)
    {
        var accessKey = _config.GetValue<string>("Storage:S3:AccessKey");
        var secretKey = _config.GetValue<string>("Storage:S3:SecretKey");
        var region = _config.GetValue<string>("Storage:S3:Region") ?? "ap-southeast-1";
        var bucket = _config.GetValue<string>("Storage:S3:BucketName") ?? "rentalboil";

        if (string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("S3 AccessKey/SecretKey not configured in appsettings.json → Storage:S3");

        using var s3 = new AmazonS3Client(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region));

        using var stream = file.OpenReadStream();
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = relativePath,
            InputStream = stream,
            ContentType = file.ContentType
        };
        await s3.PutObjectAsync(request);

        return $"https://{bucket}.s3.{region}.amazonaws.com/{relativePath}";
    }

    private async Task<bool> DeleteFromS3Async(string relativePath)
    {
        var accessKey = _config.GetValue<string>("Storage:S3:AccessKey");
        var secretKey = _config.GetValue<string>("Storage:S3:SecretKey");
        var region = _config.GetValue<string>("Storage:S3:Region") ?? "ap-southeast-1";
        var bucket = _config.GetValue<string>("Storage:S3:BucketName") ?? "rentalboil";
        if (string.IsNullOrWhiteSpace(accessKey)) return false;

        using var s3 = new AmazonS3Client(accessKey, secretKey, Amazon.RegionEndpoint.GetBySystemName(region));
        await s3.DeleteObjectAsync(bucket, relativePath);
        return true;
    }

    // ═══════════════════════ MINIO ═══════════════════════

    private async Task<string> UploadToMinIOAsync(IFormFile file, string relativePath)
    {
        var endpoint = _config.GetValue<string>("Storage:MinIO:Endpoint") ?? "localhost:9000";
        var accessKey = _config.GetValue<string>("Storage:MinIO:AccessKey") ?? "minioadmin";
        var secretKey = _config.GetValue<string>("Storage:MinIO:SecretKey") ?? "minioadmin";
        var bucket = _config.GetValue<string>("Storage:MinIO:BucketName") ?? "rentalboil";
        var useSsl = _config.GetValue<bool>("Storage:MinIO:UseSSL");

        var minio = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();

        var exists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket));
        if (!exists) await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket));

        using var stream = file.OpenReadStream();
        await minio.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(relativePath)
            .WithStreamData(stream)
            .WithObjectSize(file.Length)
            .WithContentType(file.ContentType));

        var protocol = useSsl ? "https" : "http";
        return $"{protocol}://{endpoint}/{bucket}/{relativePath}";
    }

    private async Task<bool> DeleteFromMinIOAsync(string relativePath)
    {
        var endpoint = _config.GetValue<string>("Storage:MinIO:Endpoint") ?? "localhost:9000";
        var accessKey = _config.GetValue<string>("Storage:MinIO:AccessKey") ?? "minioadmin";
        var secretKey = _config.GetValue<string>("Storage:MinIO:SecretKey") ?? "minioadmin";
        var bucket = _config.GetValue<string>("Storage:MinIO:BucketName") ?? "rentalboil";
        var useSsl = _config.GetValue<bool>("Storage:MinIO:UseSSL");

        var minio = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();

        await minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(relativePath));
        return true;
    }
}

/// <summary>
/// Service untuk manajemen tema (Dark/Light)
/// </summary>
public class ThemeService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public ThemeService(IHttpContextAccessor httpContextAccessor) { _httpContextAccessor = httpContextAccessor; }
    public string GetCurrentTheme() => _httpContextAccessor.HttpContext?.Request.Cookies["theme"] ?? "light";
    public void SetTheme(string theme) { _httpContextAccessor.HttpContext?.Response.Cookies.Append("theme", theme, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = false }); }
}

/// <summary>
/// Service untuk multi-language
/// </summary>
public class LanguageService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public LanguageService(IHttpContextAccessor httpContextAccessor) { _httpContextAccessor = httpContextAccessor; }
    public string GetCurrentLanguage() => _httpContextAccessor.HttpContext?.Request.Cookies["lang"] ?? "id";
    public void SetLanguage(string lang) { _httpContextAccessor.HttpContext?.Response.Cookies.Append("lang", lang, new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = false }); CultureInfo.CurrentCulture = new CultureInfo(lang); CultureInfo.CurrentUICulture = new CultureInfo(lang); }

    public string T(string key, string lang = "id")
    {
        var translations = new Dictionary<string, Dictionary<string, string>>
        {
            ["home"] = new() { ["id"] = "Beranda", ["en"] = "Home" },
            ["search"] = new() { ["id"] = "Cari Kendaraan", ["en"] = "Search Vehicles" },
            ["my_bookings"] = new() { ["id"] = "Pesanan Saya", ["en"] = "My Bookings" },
            ["dashboard"] = new() { ["id"] = "Dashboard", ["en"] = "Dashboard" },
            ["profile"] = new() { ["id"] = "Profil", ["en"] = "Profile" },
            ["logout"] = new() { ["id"] = "Keluar", ["en"] = "Logout" },
            ["login"] = new() { ["id"] = "Masuk", ["en"] = "Login" },
            ["register"] = new() { ["id"] = "Daftar", ["en"] = "Register" },
            ["book_now"] = new() { ["id"] = "Sewa Sekarang", ["en"] = "Book Now" },
            ["rent_per_day"] = new() { ["id"] = "/ hari", ["en"] = "/ day" },
            ["rent_per_hour"] = new() { ["id"] = "/ jam", ["en"] = "/ hour" },
            ["loading"] = new() { ["id"] = "Memuat...", ["en"] = "Loading..." },
            ["save"] = new() { ["id"] = "Simpan", ["en"] = "Save" },
            ["cancel"] = new() { ["id"] = "Batal", ["en"] = "Cancel" },
            ["delete"] = new() { ["id"] = "Hapus", ["en"] = "Delete" },
            ["edit"] = new() { ["id"] = "Edit", ["en"] = "Edit" },
            ["create"] = new() { ["id"] = "Tambah", ["en"] = "Create" },
            ["chat"] = new() { ["id"] = "Chat", ["en"] = "Chat" },
            ["notifications"] = new() { ["id"] = "Notifikasi", ["en"] = "Notifications" },
            ["faq"] = new() { ["id"] = "FAQ", ["en"] = "FAQ" },
            ["about"] = new() { ["id"] = "Tentang", ["en"] = "About" },
        };
        return translations.TryGetValue(key, out var dict) && dict.TryGetValue(lang, out var val) ? val : key;
    }
}

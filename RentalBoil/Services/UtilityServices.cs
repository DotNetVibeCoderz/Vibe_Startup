using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RentalBoil.Data;

namespace RentalBoil.Services;

/// <summary>
/// Service untuk export data ke CSV dan Excel
/// </summary>
public class ExportService
{
    private readonly AppDbContext _db;

    public ExportService(AppDbContext db) { _db = db; }

    /// <summary>
    /// Export data ke CSV
    /// </summary>
    public async Task<byte[]> ExportToCsvAsync<T>(IEnumerable<T> records)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
        await csv.WriteRecordsAsync(records);
        await writer.FlushAsync();
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Export data ke Excel (XLSX)
    /// </summary>
    public byte[] ExportToExcel<T>(IEnumerable<T> records, string sheetName = "Data")
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        // Get properties
        var properties = typeof(T).GetProperties()
            .Where(p => p.CanRead && !p.PropertyType.IsGenericType)
            .ToArray();

        // Header
        for (int i = 0; i < properties.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = properties[i].Name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4A90D9");
            cell.Style.Font.FontColor = XLColor.White;
        }

        // Data
        int row = 2;
        foreach (var record in records)
        {
            for (int col = 0; col < properties.Length; col++)
            {
                var value = properties[col].GetValue(record);
                worksheet.Cell(row, col + 1).Value = value?.ToString() ?? "";
            }
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

/// <summary>
/// Service untuk penyimpanan file (FileSystem, Azure, S3, MinIO)
/// </summary>
public class StorageService
{
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public StorageService(IConfiguration config, IWebHostEnvironment env)
    {
        _config = config;
        _env = env;
    }

    /// <summary>
    /// Upload file ke storage
    /// </summary>
    public async Task<string> UploadFileAsync(IFormFile file, string folder = "general")
    {
        var provider = _config.GetValue<string>("Storage:Provider") ?? "FileSystem";
        var basePath = _config.GetValue<string>("Storage:BasePath") ?? "wwwroot/uploads";
        var baseUrl = _config.GetValue<string>("Storage:BaseUrl") ?? "/uploads";

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var relativePath = Path.Combine(folder, fileName);
        var fullPath = Path.Combine(_env.ContentRootPath, basePath, relativePath);

        // Ensure directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        switch (provider)
        {
            case "AzureBlob":
                // Would use Azure.Storage.Blobs here
                break;
            case "S3":
                // Would use AWSSDK.S3 here
                break;
            case "MinIO":
                // Would use Minio here
                break;
            default: // FileSystem
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                break;
        }

        return $"{baseUrl}/{relativePath}";
    }

    /// <summary>
    /// Hapus file dari storage
    /// </summary>
    public Task<bool> DeleteFileAsync(string filePath)
    {
        var fullPath = Path.Combine(_env.ContentRootPath, "wwwroot", filePath.TrimStart('/'));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    /// <summary>
    /// Get list of files in a folder
    /// </summary>
    public Task<List<string>> ListFilesAsync(string folder)
    {
        var basePath = _config.GetValue<string>("Storage:BasePath") ?? "wwwroot/uploads";
        var fullPath = Path.Combine(_env.ContentRootPath, basePath, folder);

        if (!Directory.Exists(fullPath))
            return Task.FromResult(new List<string>());

        var files = Directory.GetFiles(fullPath)
            .Select(f => Path.GetRelativePath(_env.ContentRootPath, f).Replace("\\", "/"))
            .ToList();

        return Task.FromResult(files);
    }
}

/// <summary>
/// Service untuk manajemen tema (Dark/Light)
/// </summary>
public class ThemeService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ThemeService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCurrentTheme()
    {
        return _httpContextAccessor.HttpContext?.Request.Cookies["theme"] ?? "light";
    }

    public void SetTheme(string theme)
    {
        _httpContextAccessor.HttpContext?.Response.Cookies.Append("theme", theme, 
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = false });
    }
}

/// <summary>
/// Service untuk multi-language
/// </summary>
public class LanguageService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LanguageService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCurrentLanguage()
    {
        return _httpContextAccessor.HttpContext?.Request.Cookies["lang"] ?? "id";
    }

    public void SetLanguage(string lang)
    {
        _httpContextAccessor.HttpContext?.Response.Cookies.Append("lang", lang,
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), HttpOnly = false });

        CultureInfo.CurrentCulture = new CultureInfo(lang);
        CultureInfo.CurrentUICulture = new CultureInfo(lang);
    }

    /// <summary>
    /// Get localized string from resource file or dictionary
    /// </summary>
    public string T(string key, string lang = "id")
    {
        // Dictionary-based localization for core strings
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
            ["settings"] = new() { ["id"] = "Pengaturan", ["en"] = "Settings" },
            ["admin_panel"] = new() { ["id"] = "Panel Admin", ["en"] = "Admin Panel" },
            ["partner_panel"] = new() { ["id"] = "Panel Partner", ["en"] = "Partner Panel" },
            ["vehicles"] = new() { ["id"] = "Kendaraan", ["en"] = "Vehicles" },
            ["book_now"] = new() { ["id"] = "Sewa Sekarang", ["en"] = "Book Now" },
            ["rent_per_day"] = new() { ["id"] = "/ hari", ["en"] = "/ day" },
            ["rent_per_hour"] = new() { ["id"] = "/ jam", ["en"] = "/ hour" },
            ["loading"] = new() { ["id"] = "Memuat...", ["en"] = "Loading..." },
            ["save"] = new() { ["id"] = "Simpan", ["en"] = "Save" },
            ["cancel"] = new() { ["id"] = "Batal", ["en"] = "Cancel" },
            ["delete"] = new() { ["id"] = "Hapus", ["en"] = "Delete" },
            ["edit"] = new() { ["id"] = "Edit", ["en"] = "Edit" },
            ["create"] = new() { ["id"] = "Tambah", ["en"] = "Create" },
            ["filter"] = new() { ["id"] = "Filter", ["en"] = "Filter" },
            ["sort"] = new() { ["id"] = "Urutkan", ["en"] = "Sort" },
            ["export_csv"] = new() { ["id"] = "Export CSV", ["en"] = "Export CSV" },
            ["export_excel"] = new() { ["id"] = "Export Excel", ["en"] = "Export Excel" },
            ["chat"] = new() { ["id"] = "Chat", ["en"] = "Chat" },
            ["notifications"] = new() { ["id"] = "Notifikasi", ["en"] = "Notifications" },
            ["faq"] = new() { ["id"] = "FAQ", ["en"] = "FAQ" },
            ["about"] = new() { ["id"] = "Tentang", ["en"] = "About" },
        };

        return translations.TryGetValue(key, out var dict) && dict.TryGetValue(lang, out var val)
            ? val : key;
    }
}

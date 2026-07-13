using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using PCHub.Shared.Interfaces;

namespace PCHub.Shared.Services;

/// <summary>Export data ke CSV, Excel, dan PDF (HTML-based)</summary>
public class ExportService : IExportService
{
    public async Task<byte[]> ExportToCsvAsync<T>(List<T> data)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Encoding = Encoding.UTF8,
            Delimiter = ";"
        });

        await csv.WriteRecordsAsync(data);
        await writer.FlushAsync();
        return memoryStream.ToArray();
    }

    public async Task<byte[]> ExportToExcelAsync<T>(List<T> data, string sheetName = "Data")
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        worksheet.Cell(1, 1).InsertTable(data);

        var headerRange = worksheet.Range(1, 1, 1, typeof(T).GetProperties().Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1a73e8");
        headerRange.Style.Font.FontColor = XLColor.White;

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return await Task.FromResult(stream.ToArray());
    }

    /// <summary>Export data ke format PDF menggunakan HTML yang bisa dicetak</summary>
    public async Task<byte[]> ExportToPdfAsync<T>(List<T> data, string title = "Report", string? subtitle = null)
    {
        var html = GenerateHtmlReport(data, title, subtitle);
        return await Task.FromResult(Encoding.UTF8.GetBytes(html));
    }

    /// <summary>Generate laporan dalam format HTML (printable PDF-compatible)</summary>
    private static string GenerateHtmlReport<T>(List<T> data, string title, string? subtitle)
    {
        var props = typeof(T).GetProperties();
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"id\"><head><meta charset=\"UTF-8\">");
        sb.AppendLine($"<title>{title} - PCHub</title>");
        sb.AppendLine(@"<style>
            @media print { @page { size: A4 landscape; margin: 1cm; } }
            * { font-family: 'Segoe UI', Arial, Helvetica, sans-serif; }
            body { margin: 20px; color: #1A1A2E; }
            .header { border-bottom: 3px solid #1A1A2E; padding-bottom: 10px; margin-bottom: 20px; }
            .header h1 { margin: 0; font-size: 22px; color: #1A1A2E; }
            .header .subtitle { color: #65676B; font-size: 13px; }
            .header .date { color: #65676B; font-size: 11px; }
            .logo { font-size: 28px; }
            table { width: 100%; border-collapse: collapse; margin-top: 10px; }
            thead th { background: #1E293B; color: white; padding: 8px 10px; text-align: left; font-size: 10px; text-transform: uppercase; }
            tbody td { padding: 6px 10px; border-bottom: 1px solid #E2E8F0; font-size: 10px; }
            tbody tr:nth-child(even) { background: #F8FAFC; }
            .footer { margin-top: 20px; padding-top: 10px; border-top: 1px solid #E2E8F0; font-size: 10px; color: #65676B; text-align: right; }
</style></head><body>");

        // Header
        sb.AppendLine("<div class=\"header\">");
        sb.AppendLine("<div class=\"logo\">🎮 PCHub Game Center</div>");
        sb.AppendLine($"<h1>{System.Net.WebUtility.HtmlEncode(title)}</h1>");
        if (!string.IsNullOrEmpty(subtitle))
            sb.AppendLine($"<div class=\"subtitle\">{System.Net.WebUtility.HtmlEncode(subtitle)}</div>");
        sb.AppendLine($"<div class=\"date\">Generated: {DateTime.Now:dd MMM yyyy HH:mm} | Total: {data.Count} records</div>");
        sb.AppendLine("</div>");

        // Table
        sb.AppendLine("<table><thead><tr>");
        foreach (var prop in props)
            sb.AppendLine($"<th>{System.Net.WebUtility.HtmlEncode(prop.Name)}</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var item in data)
        {
            sb.AppendLine("<tr>");
            foreach (var prop in props)
            {
                var value = prop.GetValue(item)?.ToString() ?? "";
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(value)}</td>");
            }
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table>");

        // Footer
        sb.AppendLine("<div class=\"footer\">");
        sb.AppendLine("PCHub Game Center - Report &copy; 2025 | Page 1/1");
        sb.AppendLine("</div>");

        sb.AppendLine("</body></html>");

        return sb.ToString();
    }
}

/// <summary>Storage service untuk file upload</summary>
public class StorageService : IStorageService
{
    private readonly string _basePath;

    public StorageService(string basePath = "uploads")
    {
        _basePath = basePath;
        if (!Directory.Exists(_basePath))
            Directory.CreateDirectory(_basePath);
    }

    public async Task<string> UploadFileAsync(string fileName, byte[] content, string contentType)
    {
        var uniqueName = $"{Guid.NewGuid():N}_{fileName}";
        var filePath = Path.Combine(_basePath, uniqueName);
        await File.WriteAllBytesAsync(filePath, content);
        return $"/uploads/{uniqueName}";
    }

    public async Task<byte[]> DownloadFileAsync(string path)
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), path.TrimStart('/'));
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);
        return await File.ReadAllBytesAsync(filePath);
    }

    public Task<bool> DeleteFileAsync(string path)
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), path.TrimStart('/'));
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }
}

using System.Globalization;
using System.Reflection;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;
using SmartDrive.Models.ViewModels;

namespace SmartDrive.Services;

/// <summary>
/// Service untuk export data ke CSV dan Excel
/// </summary>
public class ExportService
{
    /// <summary>
    /// Export data ke CSV
    /// </summary>
    public async Task<byte[]> ExportToCsvAsync<T>(IEnumerable<T> data, string fileName)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Write header
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            csv.WriteField(prop.Name);
        }
        await csv.NextRecordAsync();

        // Write data
        foreach (var item in data)
        {
            foreach (var prop in properties)
            {
                var value = prop.GetValue(item);
                csv.WriteField(value?.ToString() ?? "");
            }
            await csv.NextRecordAsync();
        }

        await writer.FlushAsync();
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Export data ke Excel (XLSX)
    /// </summary>
    public byte[] ExportToExcel<T>(IEnumerable<T> data, string sheetName = "Data")
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // Write header
        for (int i = 0; i < properties.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = properties[i].Name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
        }

        // Write data
        int row = 2;
        foreach (var item in data)
        {
            for (int col = 0; col < properties.Length; col++)
            {
                var value = properties[col].GetValue(item);
                worksheet.Cell(row, col + 1).Value = value?.ToString() ?? "";
            }
            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Generate receipt/struk sebagai HTML untuk print
    /// </summary>
    public string GenerateReceiptHtml(string orderNumber, string customerName, List<(string item, int qty, decimal price)> items, decimal total, DateTime date)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'><title>Struk Pembayaran</title>");
        sb.AppendLine("<style>body{font-family:Arial,sans-serif;max-width:400px;margin:20px auto;padding:20px;border:1px solid #ddd;}");
        sb.AppendLine(".header{text-align:center;border-bottom:2px solid #333;padding-bottom:10px;}");
        sb.AppendLine(".item{margin:10px 0;}.total{font-weight:bold;font-size:1.2em;border-top:1px solid #333;padding-top:10px;}");
        sb.AppendLine("</style></head><body>");
        sb.AppendLine($"<div class='header'><h2>SmartDrive Academy</h2><p>Order: {orderNumber}</p><p>{date:dd MMM yyyy HH:mm}</p></div>");
        sb.AppendLine($"<p>Customer: {customerName}</p>");
        sb.AppendLine("<hr/>");
        foreach (var item in items)
        {
            sb.AppendLine($"<div class='item'>{item.item} x{item.qty} - Rp {item.price * item.qty:N0}</div>");
        }
        sb.AppendLine($"<div class='total'>Total: Rp {total:N0}</div>");
        sb.AppendLine("<p style='text-align:center;margin-top:20px;'>Terima kasih telah menggunakan SmartDrive!</p>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }
}

using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using CsvHelper;

namespace FitnessCenter.Services;

/// <summary>
/// Service untuk export data ke CSV dan Excel
/// </summary>
public class ExportService
{
    public byte[] ExportToCsv<T>(IEnumerable<T> data)
    {
        using var memoryStream = new MemoryStream();
        using var writer = new StreamWriter(memoryStream, Encoding.UTF8);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(data);
        writer.Flush();
        return memoryStream.ToArray();
    }

    public byte[] ExportToExcel<T>(IEnumerable<T> data, string sheetName = "Data")
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);
        var props = typeof(T).GetProperties();

        // Write headers with styling
        for (int col = 0; col < props.Length; col++)
        {
            var cell = worksheet.Cell(1, col + 1);
            cell.Value = props[col].Name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.Gold;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        int row = 2;
        foreach (var item in data)
        {
            for (int col = 0; col < props.Length; col++)
            {
                var value = props[col].GetValue(item);
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

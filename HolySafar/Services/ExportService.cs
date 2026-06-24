using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace HolySafar.Services;

/// <summary>
/// Service untuk export data ke CSV dan Excel
/// </summary>
public class ExportService
{
    /// <summary>
    /// Export data ke CSV
    /// </summary>
    public byte[] ExportToCsv<T>(IEnumerable<T> data)
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = ",",
            Encoding = System.Text.Encoding.UTF8
        });

        csv.WriteRecords(data);
        writer.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Export data ke Excel (XLSX)
    /// </summary>
    public byte[] ExportToExcel<T>(IEnumerable<T> data, string sheetName = "Data")
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(sheetName);

        // Get properties
        var props = typeof(T).GetProperties()
            .Where(p => p.CanRead)
            .ToList();

        // Write header
        for (int i = 0; i < props.Count; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = props[i].Name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        // Write data
        var dataList = data.ToList();
        for (int row = 0; row < dataList.Count; row++)
        {
            for (int col = 0; col < props.Count; col++)
            {
                var value = props[col].GetValue(dataList[row]);
                ws.Cell(row + 2, col + 1).Value = value?.ToString() ?? "";
                ws.Cell(row + 2, col + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
        }

        // Auto-fit columns
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}

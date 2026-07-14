using System.Text;
using BlazorViz.Models;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BlazorViz.Services;

/// <summary>Exports TableData to CSV / JSON / Excel / PDF. Chart images are captured client-side.</summary>
public sealed class ExportService
{
    static ExportService() => QuestPDF.Settings.License = LicenseType.Community;

    public (byte[] Bytes, string ContentType, string Extension) Export(TableData data, string format, string title = "Export") =>
        format.ToLowerInvariant() switch
        {
            "csv" => (Encoding.UTF8.GetBytes(data.ToCsv()), "text/csv", "csv"),
            "json" => (Encoding.UTF8.GetBytes(data.ToJson()), "application/json", "json"),
            "excel" or "xlsx" => (ToExcel(data, title), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "xlsx"),
            "pdf" => (ToPdf(data, title), "application/pdf", "pdf"),
            _ => throw new InvalidOperationException($"Unknown export format '{format}'.")
        };

    public byte[] ToExcel(TableData data, string sheetName = "Data")
    {
        using var wb = new XLWorkbook();
        var safe = string.Join("", sheetName.Where(c => !@"[]/\?*:".Contains(c)));
        var ws = wb.AddWorksheet(string.IsNullOrWhiteSpace(safe) ? "Data" : safe[..Math.Min(safe.Length, 31)]);
        for (var c = 0; c < data.Columns.Count; c++)
        {
            ws.Cell(1, c + 1).Value = data.Columns[c].Name;
            ws.Cell(1, c + 1).Style.Font.Bold = true;
        }
        for (var r = 0; r < data.Rows.Count; r++)
            for (var c = 0; c < data.Columns.Count; c++)
            {
                var v = data.Rows[r][c];
                var cell = ws.Cell(r + 2, c + 1);
                switch (v)
                {
                    case null: break;
                    case long l: cell.Value = l; break;
                    case double d: cell.Value = d; break;
                    case bool b: cell.Value = b; break;
                    case DateTime dt: cell.Value = dt; break;
                    default: cell.Value = TableData.Format(v); break;
                }
            }
        ws.Columns().AdjustToContents(1, Math.Min(data.Rows.Count + 1, 100));
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ToPdf(TableData data, string title)
    {
        const int maxCols = 12;
        var cols = data.Columns.Take(maxCols).ToList();
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(cols.Count > 6 ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(8));
                page.Header().Text(title).FontSize(16).Bold();
                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        foreach (var _ in cols) c.RelativeColumn();
                    });
                    table.Header(header =>
                    {
                        foreach (var col in cols)
                            header.Cell().Background(Colors.Grey.Lighten2).Padding(4).Text(col.Name).Bold();
                    });
                    foreach (var row in data.Rows.Take(2000))
                        for (var i = 0; i < cols.Count; i++)
                            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                                .Text(TableData.Format(row[data.IndexOf(cols[i].Name)]));
                });
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("BlazorViz — ").FontSize(7);
                    t.CurrentPageNumber().FontSize(7);
                    t.Span(" / ").FontSize(7);
                    t.TotalPages().FontSize(7);
                });
            });
        });
        return doc.GeneratePdf();
    }
}

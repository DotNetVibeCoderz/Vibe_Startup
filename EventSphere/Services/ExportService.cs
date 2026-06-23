using System.Globalization;
using CsvHelper;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using EventSphere.Data.Context;

namespace EventSphere.Services;

/// <summary>
/// Export data dalam format CSV dan Excel
/// </summary>
public class ExportService
{
    private readonly AppDbContext _db;
    public ExportService(AppDbContext db) => _db = db;

    /// <summary>
    /// Export data ke CSV
    /// </summary>
    public async Task<byte[]> ExportToCsvAsync<T>(IEnumerable<T> records) where T : class
    {
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        await csv.WriteRecordsAsync(records);
        await writer.FlushAsync();
        return ms.ToArray();
    }

    /// <summary>
    /// Export data ke Excel (ClosedXML)
    /// </summary>
    public async Task<byte[]> ExportToExcelAsync<T>(IEnumerable<T> records, string sheetName = "Data") where T : class
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(sheetName);
        ws.Cell(1, 1).InsertTable(records);
        ws.Columns().AdjustToContents();
        
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return await Task.FromResult(ms.ToArray());
    }

    /// <summary>
    /// Generate laporan event (keuangan, kehadiran, feedback)
    /// </summary>
    public async Task<byte[]> GenerateEventReportAsync(Guid eventId)
    {
        var evt = await _db.Events
            .Include(e => e.BudgetItems)
            .Include(e => e.Attendees).ThenInclude(a => a.User)
            .Include(e => e.Feedbacks)
            .Include(e => e.VendorContracts).ThenInclude(vc => vc.Vendor)
            .FirstOrDefaultAsync(e => e.Id == eventId);

        using var wb = new XLWorkbook();

        // Sheet 1: Event Summary
        var ws1 = wb.Worksheets.Add("Summary");
        ws1.Cell(1, 1).Value = "Event Report";
        ws1.Cell(2, 1).Value = "Event Name"; ws1.Cell(2, 2).Value = evt?.Name;
        ws1.Cell(3, 1).Value = "Date"; ws1.Cell(3, 2).Value = evt?.EventDate.ToString("dd MMM yyyy");
        ws1.Cell(4, 1).Value = "Status"; ws1.Cell(4, 2).Value = evt?.Status.ToString();
        ws1.Cell(5, 1).Value = "Budget Total"; ws1.Cell(5, 2).Value = evt?.BudgetTotal;
        ws1.Cell(6, 1).Value = "Budget Spent"; ws1.Cell(6, 2).Value = evt?.BudgetSpent;
        ws1.Cell(7, 1).Value = "Guests Expected"; ws1.Cell(7, 2).Value = evt?.ExpectedGuests;
        ws1.Cell(8, 1).Value = "Guests Confirmed"; ws1.Cell(8, 2).Value = evt?.ConfirmedGuests;

        // Sheet 2: Budget
        var ws2 = wb.Worksheets.Add("Budget");
        if (evt?.BudgetItems != null)
            ws2.Cell(1, 1).InsertTable(evt.BudgetItems.Select(b => new { b.Name, b.Category, b.EstimatedCost, b.ActualCost, b.IsPaid }));

        // Sheet 3: Attendees
        var ws3 = wb.Worksheets.Add("Attendees");
        if (evt?.Attendees != null)
            ws3.Cell(1, 1).InsertTable(evt.Attendees.Select(a => new { Name = a.User?.FullName, a.Role, a.RsvpStatus, a.DietaryRestrictions }));

        // Sheet 4: Feedback
        var ws4 = wb.Worksheets.Add("Feedback");
        if (evt?.Feedbacks != null)
            ws4.Cell(1, 1).InsertTable(evt.Feedbacks.Select(f => new { f.Rating, f.Comment, f.Category }));

        ws1.Columns().AdjustToContents();
        ws2.Columns().AdjustToContents();
        ws3.Columns().AdjustToContents();
        ws4.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}

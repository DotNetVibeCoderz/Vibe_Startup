using System.Text;
using FastRide.Api.Services;

namespace FastRide.Tests.Unit;

public class CsvExporterTests
{
    private sealed record Row(string Name, decimal Amount, DateTime When, bool Flag, string? Missing = null);

    private static string Render(byte[] csv) => Encoding.UTF8.GetString(csv);

    private static byte[] Build(params Row[] rows) => CsvExporter.Build(rows,
        ("Nama", r => r.Name),
        ("Jumlah", r => r.Amount),
        ("Waktu", r => r.When),
        ("Aktif", r => r.Flag),
        ("Kosong", r => r.Missing));

    [Fact]
    public void Build_StartsWithAUtf8Bom()
    {
        // Without the BOM, Excel on Windows mangles Indonesian text.
        var csv = Build(new Row("Budi", 1000m, new DateTime(2026, 8, 4, 10, 30, 0), true));

        Assert.Equal(0xEF, csv[0]);
        Assert.Equal(0xBB, csv[1]);
        Assert.Equal(0xBF, csv[2]);
    }

    [Fact]
    public void Build_WritesTheHeaderRow()
    {
        var text = Render(Build(new Row("Budi", 1m, DateTime.UnixEpoch, false)));

        Assert.Contains("Nama,Jumlah,Waktu,Aktif,Kosong", text);
    }

    [Fact]
    public void Build_QuotesValuesContainingASeparator()
    {
        var text = Render(Build(new Row("Jl. Sudirman No. 1, Jakarta", 1m, DateTime.UnixEpoch, false)));

        Assert.Contains("\"Jl. Sudirman No. 1, Jakarta\"", text);
    }

    [Fact]
    public void Build_DoublesEmbeddedQuotes()
    {
        var text = Render(Build(new Row("Driver \"Andi\"", 1m, DateTime.UnixEpoch, false)));

        Assert.Contains("\"Driver \"\"Andi\"\"\"", text);
    }

    [Fact]
    public void Build_QuotesValuesContainingNewlines()
    {
        var text = Render(Build(new Row("Baris satu\nBaris dua", 1m, DateTime.UnixEpoch, false)));

        Assert.Contains("\"Baris satu\nBaris dua\"", text);
    }

    [Fact]
    public void Build_FormatsNumbersInvariantly()
    {
        // A comma decimal separator would split the cell.
        var text = Render(Build(new Row("Budi", 1234.5m, DateTime.UnixEpoch, false)));

        Assert.Contains("1234.5", text);
        Assert.DoesNotContain("1234,5", text);
    }

    [Fact]
    public void Build_FormatsDatesSortably()
    {
        var text = Render(Build(new Row("Budi", 1m, new DateTime(2026, 8, 4, 17, 5, 9), false)));

        Assert.Contains("2026-08-04 17:05:09", text);
    }

    [Fact]
    public void Build_WritesNullAsAnEmptyCell()
    {
        var text = Render(Build(new Row("Budi", 1m, DateTime.UnixEpoch, false)));

        var dataLine = text
            .Split('\n')
            .First(line => line.StartsWith("Budi", StringComparison.Ordinal))
            .TrimEnd('\r');

        // The last column is null, so the line ends on an empty cell.
        Assert.EndsWith(",", dataLine);
    }

    [Fact]
    public void Build_WritesBooleansAsTrueOrFalse()
    {
        var text = Render(Build(new Row("Budi", 1m, DateTime.UnixEpoch, true)));

        Assert.Contains("true,", text);
    }

    [Fact]
    public void Build_WritesOneLinePerRow()
    {
        var csv = Build(
            new Row("A", 1m, DateTime.UnixEpoch, true),
            new Row("B", 2m, DateTime.UnixEpoch, false),
            new Row("C", 3m, DateTime.UnixEpoch, true));

        var lines = Render(csv)
            .TrimStart('﻿')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith("sep=", StringComparison.Ordinal))
            .ToList();

        // header + three rows
        Assert.Equal(4, lines.Count);
    }

    [Fact]
    public void Build_HandlesAnEmptySequence()
    {
        var text = Render(CsvExporter.Build(Array.Empty<Row>(), ("Nama", r => r.Name)));

        Assert.Contains("Nama", text);
    }
}

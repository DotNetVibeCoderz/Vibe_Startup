using System.Globalization;
using System.Text;

namespace FastRide.Api.Services;

/// <summary>
/// Minimal RFC 4180 CSV writer for the dashboard's export buttons.
/// A UTF-8 BOM is written so Excel on Windows opens Indonesian text correctly instead of
/// mangling it, and a separator hint keeps it from splitting on the wrong character.
/// </summary>
public static class CsvExporter
{
    public static byte[] Build<T>(IEnumerable<T> rows, params (string Header, Func<T, object?> Value)[] columns)
    {
        var builder = new StringBuilder();
        builder.Append("sep=,\n");
        builder.AppendLine(string.Join(',', columns.Select(c => Escape(c.Header))));

        foreach (var row in rows)
            builder.AppendLine(string.Join(',', columns.Select(c => Escape(Format(c.Value(row))))));

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        decimal number => number.ToString("0.##", CultureInfo.InvariantCulture),
        double number => number.ToString("0.##", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        _ => value.ToString() ?? string.Empty
    };

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}

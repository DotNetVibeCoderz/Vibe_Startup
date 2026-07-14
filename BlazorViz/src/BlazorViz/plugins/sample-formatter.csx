// Sample BlazorViz plugin: custom kernel functions for the Data Wizard.
// Any .csx file in this folder is loaded at startup. The script must END with
// an expression returning an object whose [KernelFunction] methods become tools.

using System.ComponentModel;
using Microsoft.SemanticKernel;

public class FormatterPlugin
{
    [KernelFunction("format_currency")]
    [Description("Formats a number as currency, e.g. 1234567.89 with code 'IDR' → 'Rp 1.234.567,89'.")]
    public string FormatCurrency(double amount, [Description("ISO currency code: IDR, USD, EUR…")] string code = "USD")
    {
        return code.ToUpperInvariant() switch
        {
            "IDR" => "Rp " + amount.ToString("N2", new System.Globalization.CultureInfo("id-ID")),
            "EUR" => amount.ToString("C2", new System.Globalization.CultureInfo("de-DE")),
            _ => amount.ToString("C2", new System.Globalization.CultureInfo("en-US"))
        };
    }

    [KernelFunction("format_bytes")]
    [Description("Formats a byte count as a human readable size (KB, MB, GB…).")]
    public string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.##} {units[unit]}";
    }

    [KernelFunction("check_indonesian_holiday")]
    [Description("Checks whether a date (yyyy-MM-dd) falls on a fixed Indonesian national holiday.")]
    public string CheckHoliday(string date)
    {
        if (!DateTime.TryParse(date, out var d)) return "Invalid date.";
        var fixedHolidays = new Dictionary<string, string>
        {
            ["01-01"] = "Tahun Baru Masehi",
            ["05-01"] = "Hari Buruh Internasional",
            ["06-01"] = "Hari Lahir Pancasila",
            ["08-17"] = "Hari Kemerdekaan RI",
            ["12-25"] = "Hari Raya Natal"
        };
        return fixedHolidays.TryGetValue(d.ToString("MM-dd"), out var name)
            ? $"{date} is a national holiday: {name}"
            : $"{date} is not a fixed national holiday (movable religious holidays not included).";
    }
}

return new FormatterPlugin();

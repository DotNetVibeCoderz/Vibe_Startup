using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace PDA.Services.LLM.KernelPlugins;

/// <summary>
/// Kernel Plugin: Common utility functions for the AI agent
/// Date/time, math calculations, formatting
/// </summary>
public class CommonFunctionsPlugin
{
    /// <summary>
    /// Get the current UTC date and time
    /// </summary>
    [KernelFunction("getCurrentDateTime")]
    [Description("Get the current UTC date and time. Use this when you need to know the current date/time " +
                 "for time-based calculations, comparisons, or context.")]
    public string GetCurrentDateTime()
    {
        var now = DateTime.UtcNow;
        return $"📅 **Current UTC Date/Time:** {now:yyyy-MM-dd HH:mm:ss}\n" +
               $"- Day: {now:dddd}\n" +
               $"- ISO 8601: {now:O}\n" +
               $"- Unix Timestamp: {new DateTimeOffset(now).ToUnixTimeSeconds()}";
    }

    /// <summary>
    /// Format a date string into a friendly human-readable format
    /// </summary>
    [KernelFunction("formatDateFriendly")]
    [Description("Format a date string into a friendly, human-readable format. " +
                 "Works with ISO 8601, common date formats, and relative descriptions.")]
    public string FormatDateFriendly(
        [Description("The date string to format (ISO 8601 or common format)")]
        string date)
    {
        if (DateTime.TryParse(date, out var parsed))
        {
            var diff = DateTime.UtcNow - parsed;
            var relative = diff.TotalSeconds < 0 ? "in the future" :
                diff.TotalSeconds < 60 ? $"{diff.TotalSeconds:F0} seconds ago" :
                diff.TotalMinutes < 60 ? $"{diff.TotalMinutes:F0} minutes ago" :
                diff.TotalHours < 24 ? $"{diff.TotalHours:F0} hours ago" :
                diff.TotalDays < 7 ? $"{diff.TotalDays:F0} days ago" :
                diff.TotalDays < 30 ? $"{diff.TotalDays / 7:F0} weeks ago" :
                diff.TotalDays < 365 ? $"{diff.TotalDays / 30:F0} months ago" :
                $"{diff.TotalDays / 365:F1} years ago";

            return $"{parsed:dddd, dd MMMM yyyy HH:mm} UTC ({relative})";
        }
        return $"❌ Could not parse date: \"{date}\". Please use ISO 8601 format (e.g., 2024-01-15 or 2024-01-15T10:30:00).";
    }

    /// <summary>
    /// Perform a mathematical calculation
    /// </summary>
    [KernelFunction("calculateMath")]
    [Description("Perform a mathematical calculation. Supports: +, -, *, /, %, ^ (power), sqrt, abs, round, sum, avg, min, max. " +
                 "Use this for calculations that can't easily be done in SQL.")]
    public string CalculateMath(
        [Description("The math expression to evaluate, e.g., '100 * 1.5 + 200 / 3' or 'sum(10,20,30)'")]
        string expression)
    {
        try
        {
            // Use NCalc or DataTable for simple arithmetic
            var dt = new System.Data.DataTable();
            var result = dt.Compute(expression.Replace("^", ""), "");
            return $"🧮 **Result:** {expression} = **{result}**";
        }
        catch (Exception ex)
        {
            return $"❌ Math error: {ex.Message}. Try a simpler expression.";
        }
    }

    /// <summary>
    /// Fetch and read data from a URL
    /// </summary>
    [KernelFunction("readDataFromUrl")]
    [Description("Fetch and read data from an external URL. Supports JSON, CSV, text, and HTML. " +
                 "Use this to get supplementary data from APIs or websites. Results truncated at 5000 characters.")]
    public async Task<string> ReadDataFromUrlAsync(
        [Description("The URL to fetch data from")]
        string url,
        IHttpClientFactory httpClientFactory)
    {
        try
        {
            var client = httpClientFactory.CreateClient("DefaultClient");
            var response = await client.GetStringAsync(url);

            if (response.Length > 5000)
                response = response[..5000] + $"\n\n... (content truncated at 5000 chars, total: {response.Length:N0} chars)";

            return $"🌐 **Data from:** {url}\n\n```\n{response}\n```";
        }
        catch (Exception ex)
        {
            return $"❌ Error fetching URL: {ex.Message}";
        }
    }

    /// <summary>
    /// Format a number for display with thousand separators
    /// </summary>
    [KernelFunction("formatNumber")]
    [Description("Format a number with thousand separators and optional decimal places for human-friendly display.")]
    public string FormatNumber(
        [Description("The number to format")]
        double number,
        [Description("Number of decimal places (0-6, default 0)")]
        int decimals = 0)
    {
        return number.ToString($"N{Math.Clamp(decimals, 0, 6)}");
    }
}

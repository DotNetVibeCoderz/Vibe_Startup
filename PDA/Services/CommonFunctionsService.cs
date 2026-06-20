using System.Globalization;

namespace PDA.Services;

/// <summary>
/// Common utility functions available to the LLM agent and the application
/// </summary>
public class CommonFunctionsService
{
    private readonly ILogger<CommonFunctionsService> _logger;

    public CommonFunctionsService(ILogger<CommonFunctionsService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get current date and time in various formats
    /// </summary>
    public string GetCurrentDateTime(string format = "yyyy-MM-dd HH:mm:ss")
    {
        return DateTime.UtcNow.ToString(format);
    }

    /// <summary>
    /// Format date to friendly Indonesian format
    /// </summary>
    public string FormatDateFriendly(DateTime date)
    {
        var culture = new CultureInfo("id-ID");
        return date.ToString("dddd, dd MMMM yyyy HH:mm", culture);
    }

    /// <summary>
    /// Get relative time description (e.g., "2 hours ago")
    /// </summary>
    public string GetRelativeTime(DateTime dateTime)
    {
        var span = DateTime.UtcNow - dateTime;

        if (span.TotalSeconds < 60) return "Baru saja";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} menit yang lalu";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} jam yang lalu";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} hari yang lalu";
        if (span.TotalDays < 30) return $"{(int)(span.TotalDays / 7)} minggu yang lalu";
        if (span.TotalDays < 365) return $"{(int)(span.TotalDays / 30)} bulan yang lalu";
        return $"{(int)(span.TotalDays / 365)} tahun yang lalu";
    }

    /// <summary>
    /// Format file size human readable
    /// </summary>
    public string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Format large number with separators
    /// </summary>
    public string FormatNumber(long number)
    {
        return number.ToString("N0", new CultureInfo("id-ID"));
    }

    /// <summary>
    /// Calculate percentage and format
    /// </summary>
    public string FormatPercentage(double value, double total)
    {
        if (total == 0) return "0%";
        return $"{(value / total * 100):0.0}%";
    }

    /// <summary>
    /// Truncate text with ellipsis
    /// </summary>
    public string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "...";
    }

    /// <summary>
    /// Generate a random color for charts
    /// </summary>
    public string GetRandomColor(int seed)
    {
        var colors = new[]
        {
            "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7",
            "#DDA0DD", "#98D8C8", "#F7DC6F", "#BB8FCE", "#85C1E9",
            "#F8C471", "#82E0AA", "#F1948A", "#85929E", "#AED6F1",
            "#E59866", "#ABEBC6", "#F5B7B1", "#7FB3D8", "#D2B4DE"
        };
        return colors[Math.Abs(seed) % colors.Length];
    }
}

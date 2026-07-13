using System.Security.Cryptography;
using System.Text;

namespace PCHub.Shared.Utilities;

/// <summary>Common utility functions</summary>
public static class Helpers
{
    /// <summary>Format currency to Rupiah</summary>
    public static string FormatCurrency(decimal amount)
    {
        return $"Rp {amount:N0}";
    }

    /// <summary>Format duration in minutes to readable string</summary>
    public static string FormatDuration(int minutes)
    {
        if (minutes < 60) return $"{minutes} menit";
        var hours = minutes / 60;
        var mins = minutes % 60;
        return mins > 0 ? $"{hours} jam {mins} menit" : $"{hours} jam";
    }

    /// <summary>Generate random string for tokens/codes</summary>
    public static string GenerateRandomCode(int length = 8)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, length).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }

    /// <summary>Compute SHA256 hash</summary>
    public static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    /// <summary>Truncate string with ellipsis</summary>
    public static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            return value ?? "";
        return value[..(maxLength - 3)] + "...";
    }

    /// <summary>Get relative time description</summary>
    public static string GetRelativeTime(DateTime dateTime)
    {
        var span = DateTime.UtcNow - dateTime;

        if (span.TotalMinutes < 1) return "Baru saja";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} menit yang lalu";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} jam yang lalu";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} hari yang lalu";
        if (span.TotalDays < 30) return $"{(int)(span.TotalDays / 7)} minggu yang lalu";
        if (span.TotalDays < 365) return $"{(int)(span.TotalDays / 30)} bulan yang lalu";
        return $"{(int)(span.TotalDays / 365)} tahun yang lalu";
    }
}

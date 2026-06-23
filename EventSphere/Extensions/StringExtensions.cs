namespace EventSphere;

/// <summary>
/// String helper extension methods
/// </summary>
public static class StringExtensions
{
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value ?? "";
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }
}

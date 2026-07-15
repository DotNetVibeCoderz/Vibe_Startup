using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppBender.Core.Common;

/// <summary>Central JSON helpers so every subsystem serializes consistently.</summary>
public static class JsonUtil
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static readonly JsonSerializerOptions Indented = new(Options) { WriteIndented = true };

    public static string Serialize<T>(T value, bool indented = false)
        => JsonSerializer.Serialize(value, indented ? Indented : Options);

    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, Options); }
        catch { return default; }
    }

    public static T DeserializeOrNew<T>(string? json) where T : new()
        => Deserialize<T>(json) ?? new T();

    /// <summary>Converts a JsonElement to a plain CLR object (string/double/bool/list/dictionary).</summary>
    public static object? ToClr(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) && el.GetRawText().IndexOf('.') < 0 ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array => el.EnumerateArray().Select(ToClr).ToList(),
        JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => ToClr(p.Value)),
        _ => null
    };

    public static Dictionary<string, object?> ToClrDictionary(string? json)
    {
        var doc = Deserialize<Dictionary<string, JsonElement>>(json) ?? [];
        return doc.ToDictionary(kv => kv.Key, kv => ToClr(kv.Value));
    }
}

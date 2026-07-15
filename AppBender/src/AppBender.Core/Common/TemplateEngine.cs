using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AppBender.Core.Common;

/// <summary>
/// Resolves {{path}} placeholders against a nested context (dictionaries, lists, JsonElement).
/// Paths: trigger.body.name, vars.total, steps.step1.items[0].id, plus built-ins
/// {{utcNow}}, {{today}}, {{guid}}, {{rand}}.
/// </summary>
public static partial class TemplateEngine
{
    [GeneratedRegex(@"\{\{\s*([^{}]+?)\s*\}\}")]
    private static partial Regex Placeholder();

    public static string Render(string? template, IDictionary<string, object?> context)
    {
        if (string.IsNullOrEmpty(template)) return "";
        return Placeholder().Replace(template, m =>
        {
            var value = Evaluate(m.Groups[1].Value, context);
            return ToText(value);
        });
    }

    /// <summary>Evaluates a single expression; returns the raw value (not stringified) when the whole template is one placeholder.</summary>
    public static object? EvaluateValue(string? template, IDictionary<string, object?> context)
    {
        if (string.IsNullOrEmpty(template)) return template;
        var match = Placeholder().Match(template);
        if (match.Success && match.Length == template.Trim().Length)
            return Evaluate(match.Groups[1].Value, context);
        return Render(template, context);
    }

    public static object? Evaluate(string path, IDictionary<string, object?> context)
    {
        path = path.Trim();
        switch (path)
        {
            case "utcNow": return DateTime.UtcNow.ToString("O");
            case "now": return DateTime.Now.ToString("O");
            case "today": return DateTime.Today.ToString("yyyy-MM-dd");
            case "guid": return Guid.NewGuid().ToString("N");
            case "rand": return Random.Shared.Next(0, 1_000_000);
        }

        object? current = context;
        foreach (var segment in SplitPath(path))
        {
            if (current is null) return null;
            current = Unwrap(current);
            if (segment.Index is int idx)
            {
                current = current switch
                {
                    IList<object?> list => idx >= 0 && idx < list.Count ? list[idx] : null,
                    JsonElement { ValueKind: JsonValueKind.Array } je => idx >= 0 && idx < je.GetArrayLength() ? (object?)je[idx] : null,
                    _ => null
                };
            }
            else
            {
                current = current switch
                {
                    IDictionary<string, object?> dict => dict.TryGetValue(segment.Name!, out var v) ? v : Alt(dict, segment.Name!),
                    JsonElement { ValueKind: JsonValueKind.Object } je =>
                        je.TryGetProperty(segment.Name!, out var p) ? (object?)p : null,
                    _ => Reflect(current, segment.Name!)
                };
            }
        }
        return Unwrap(current);
    }

    private static object? Alt(IDictionary<string, object?> dict, string name)
    {
        var hit = dict.Keys.FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
        return hit is null ? null : dict[hit];
    }

    private static object? Reflect(object target, string name)
    {
        var prop = target.GetType().GetProperty(name,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
        return prop?.GetValue(target);
    }

    private static object? Unwrap(object? value)
    {
        if (value is JsonElement je)
            return je.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? je : JsonUtil.ToClr(je);
        return value;
    }

    private record struct Segment(string? Name, int? Index);

    private static IEnumerable<Segment> SplitPath(string path)
    {
        foreach (var raw in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = raw;
            while (true)
            {
                var bracket = part.IndexOf('[');
                if (bracket < 0) { if (part.Length > 0) yield return new Segment(part, null); break; }
                if (bracket > 0) yield return new Segment(part[..bracket], null);
                var close = part.IndexOf(']', bracket);
                if (close < 0) break;
                if (int.TryParse(part[(bracket + 1)..close], out var idx)) yield return new Segment(null, idx);
                part = part[(close + 1)..];
                if (part.Length == 0) break;
            }
        }
    }

    public static string ToText(object? value) => value switch
    {
        null => "",
        string s => s,
        bool b => b ? "true" : "false",
        JsonElement je => je.ValueKind == JsonValueKind.String ? je.GetString() ?? "" : je.GetRawText(),
        IDictionary<string, object?> or IList<object?> => JsonUtil.Serialize(value),
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? ""
    };

    /// <summary>Renders every value of a config dictionary.</summary>
    public static Dictionary<string, string> RenderAll(IDictionary<string, string> config, IDictionary<string, object?> context)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in config) result[k] = Render(v, context);
        return result;
    }
}

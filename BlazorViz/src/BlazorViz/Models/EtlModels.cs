using System.Text.Json;

namespace BlazorViz.Models;

/// <summary>
/// One ETL transformation step. Op decides which keys of P are used:
///  filter    – field, op (=,!=,&gt;,&gt;=,&lt;,&lt;=,contains,startswith,endswith,in,notnull,isnull), value
///  select    – fields (comma separated)
///  drop      – fields (comma separated)
///  rename    – from, to
///  compute   – name, expr (JavaScript expression, row fields as variables)
///  sort      – field, desc (true/false)
///  aggregate – groupBy (comma separated), aggs ("sum:Amount, avg:Price, count:*")
///  join      – rightDatasetId, leftKey, rightKey, type (inner/left), prefix
///  limit     – n
///  distinct  – (no params)
///  pivot     – rowField, columnField, valueField, agg
/// </summary>
public sealed class EtlStep
{
    public string Op { get; set; } = "filter";
    public Dictionary<string, string> P { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string Get(string key, string fallback = "") => P.TryGetValue(key, out var v) ? v : fallback;

    public static List<EtlStep> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<EtlStep>>(json, TableData.JsonOpts) ?? []; }
        catch { return []; }
    }

    public static string ToJson(List<EtlStep> steps) => JsonSerializer.Serialize(steps, TableData.JsonOpts);
}

public sealed class ScriptTemplate
{
    public string Name { get; set; } = "";
    public string Language { get; set; } = "csharp";
    public string Description { get; set; } = "";
    public string Code { get; set; } = "";
}

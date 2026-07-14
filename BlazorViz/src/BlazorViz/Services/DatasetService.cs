using BlazorViz.Data;
using BlazorViz.Models;
using BlazorViz.Services.Connectors;
using BlazorViz.Services.Scripting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BlazorViz.Services;

/// <summary>
/// Executes datasets end-to-end: source (connection or uploaded file) → ETL steps → script,
/// with in-memory caching. Cache TTL = RefreshIntervalSeconds (falls back to 10 minutes).
/// </summary>
public sealed class DatasetService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    DataConnectorFactory connectors,
    EtlService etl,
    ScriptRunnerFactory scriptRunners,
    IMemoryCache cache,
    UsageService usage)
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);

    public async Task<TableData> GetDataAsync(int datasetId, bool forceRefresh = false, CancellationToken ct = default)
    {
        var key = $"dataset:{datasetId}";
        if (!forceRefresh && cache.TryGetValue(key, out TableData? cached) && cached is not null)
            return cached;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var ds = await db.Datasets.Include(d => d.Connection).FirstOrDefaultAsync(d => d.Id == datasetId, ct)
                 ?? throw new InvalidOperationException($"Dataset {datasetId} not found.");

        var table = await ExecuteAsync(ds, ct, visited: [datasetId]);

        ds.LastRefreshedUtc = DateTime.UtcNow;
        ds.SchemaJson = table.SchemaJson();
        await db.SaveChangesAsync(ct);

        var ttl = ds.RefreshIntervalSeconds > 0 ? TimeSpan.FromSeconds(ds.RefreshIntervalSeconds) : DefaultTtl;
        cache.Set(key, table, ttl);
        usage.Record("query", 1, meta: ds.Name);
        return table;
    }

    public void Invalidate(int datasetId) => cache.Remove($"dataset:{datasetId}");

    /// <summary>Runs a dataset definition without caching — used for previews in editors.</summary>
    public async Task<TableData> ExecuteAsync(Dataset ds, CancellationToken ct = default, HashSet<int>? visited = null)
    {
        visited ??= [];
        var table = await LoadSourceAsync(ds, ct);

        var steps = EtlStep.ParseList(ds.EtlJson);
        if (steps.Count > 0)
            table = await etl.ApplyAsync(table, steps, async rightId =>
            {
                if (!visited.Add(rightId))
                    throw new InvalidOperationException($"Circular dataset join detected (dataset {rightId}).");
                return await GetDataAsync(rightId, false, ct);
            });

        if (!string.IsNullOrWhiteSpace(ds.Script) && !string.IsNullOrWhiteSpace(ds.ScriptLanguage))
            table = await scriptRunners.Get(ds.ScriptLanguage).RunAsync(ds.Script, table, ct);

        return table;
    }

    private async Task<TableData> LoadSourceAsync(Dataset ds, CancellationToken ct)
    {
        if (ds.SourceKind == "file" || (!string.IsNullOrWhiteSpace(ds.FilePath) && ds.ConnectionId is null))
        {
            var path = ds.FilePath ?? throw new InvalidOperationException("Dataset has no file path.");
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".csv" or ".txt" => await connectors.Get("csv").QueryAsync($$"""{"path": {{System.Text.Json.JsonSerializer.Serialize(path)}}}""", null, ct),
                ".xlsx" or ".xlsm" => await connectors.Get("excel").QueryAsync($$"""{"path": {{System.Text.Json.JsonSerializer.Serialize(path)}}}""", ds.Query, ct),
                ".json" => RestConnector.JsonToTable(await File.ReadAllTextAsync(path, ct), null),
                _ => throw new InvalidOperationException($"Unsupported file type '{ext}'.")
            };
        }

        var conn = ds.Connection ?? throw new InvalidOperationException("Dataset has no connection.");
        return await connectors.Get(conn.Kind).QueryAsync(conn.ConfigJson, ds.Query, ct);
    }
}

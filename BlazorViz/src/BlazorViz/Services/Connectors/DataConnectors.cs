using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using BlazorViz.Models;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace BlazorViz.Services.Connectors;

public interface IDataConnector
{
    string Kind { get; }
    Task<TableData> QueryAsync(string configJson, string? query, CancellationToken ct = default);
    Task<(bool Ok, string Message)> TestAsync(string configJson, CancellationToken ct = default);
}

public static class ConnectorConfig
{
    public static string GetString(string configJson, string key, string fallback = "")
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty(key, out var el))
                return el.ValueKind == JsonValueKind.String ? el.GetString() ?? fallback : el.GetRawText();
        }
        catch { /* invalid config json → fallback */ }
        return fallback;
    }

    public static Dictionary<string, string> GetMap(string configJson, string key)
    {
        var result = new Dictionary<string, string>();
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            if (doc.RootElement.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Object)
                foreach (var p in el.EnumerateObject())
                    result[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() ?? "" : p.Value.GetRawText();
        }
        catch { }
        return result;
    }
}

/// <summary>Shared ADO.NET implementation for all relational providers.</summary>
public abstract class RelationalConnector(string kind) : IDataConnector
{
    public string Kind => kind;

    protected abstract DbConnection CreateConnection(string connectionString);

    public async Task<TableData> QueryAsync(string configJson, string? query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) throw new InvalidOperationException("Query (SQL) is required for database connections.");
        var cs = ConnectorConfig.GetString(configJson, "connectionString");
        await using var conn = CreateConnection(cs);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = query;
        cmd.CommandTimeout = 120;
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var table = new TableData();
        for (var i = 0; i < reader.FieldCount; i++)
            table.Columns.Add(new ColumnDef { Name = reader.GetName(i), Type = MapType(reader.GetFieldType(i)) });
        while (await reader.ReadAsync(ct))
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
                row[i] = reader.IsDBNull(i) ? null : TableData.Normalize(reader.GetValue(i));
            table.Rows.Add(row);
        }
        return table;
    }

    public async Task<(bool, string)> TestAsync(string configJson, CancellationToken ct = default)
    {
        try
        {
            await using var conn = CreateConnection(ConnectorConfig.GetString(configJson, "connectionString"));
            await conn.OpenAsync(ct);
            return (true, $"Connected to {conn.Database ?? "database"} ({Kind}).");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private static string MapType(Type t)
    {
        if (t == typeof(bool)) return "boolean";
        if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return "datetime";
        if (t == typeof(byte) || t == typeof(short) || t == typeof(int) || t == typeof(long)) return "integer";
        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal)) return "number";
        return "string";
    }
}

public sealed class SqliteConnector() : RelationalConnector("sqlite")
{
    protected override DbConnection CreateConnection(string cs) =>
        new SqliteConnection(cs.Contains('=') ? cs : $"Data Source={cs}");
}

public sealed class SqlServerConnector() : RelationalConnector("sqlserver")
{
    protected override DbConnection CreateConnection(string cs) => new SqlConnection(cs);
}

public sealed class PostgresConnector() : RelationalConnector("postgresql")
{
    protected override DbConnection CreateConnection(string cs) => new NpgsqlConnection(cs);
}

public sealed class MySqlDbConnector() : RelationalConnector("mysql")
{
    protected override DbConnection CreateConnection(string cs) => new MySqlConnection(cs);
}

public sealed class OracleConnector() : RelationalConnector("oracle")
{
    protected override DbConnection CreateConnection(string cs) => new OracleConnection(cs);
}

/// <summary>Reads .xlsx via ClosedXML. Config: { "path": "...", "sheet": "Sheet1" }. Query can override sheet name.</summary>
public sealed class ExcelConnector : IDataConnector
{
    public string Kind => "excel";

    public Task<TableData> QueryAsync(string configJson, string? query, CancellationToken ct = default)
    {
        var path = ConnectorConfig.GetString(configJson, "path");
        var sheet = string.IsNullOrWhiteSpace(query) ? ConnectorConfig.GetString(configJson, "sheet") : query.Trim();
        using var wb = new XLWorkbook(path);
        var ws = string.IsNullOrWhiteSpace(sheet) ? wb.Worksheets.First() : wb.Worksheet(sheet);
        var range = ws.RangeUsed();
        var table = new TableData();
        if (range is null) return Task.FromResult(table);

        var firstRow = range.FirstRow();
        foreach (var cell in firstRow.Cells())
            table.Columns.Add(new ColumnDef { Name = cell.GetString() });
        foreach (var row in range.Rows().Skip(1))
        {
            var arr = new object?[table.Columns.Count];
            for (var i = 0; i < table.Columns.Count; i++)
            {
                var cell = row.Cell(i + 1);
                arr[i] = cell.Value.Type switch
                {
                    XLDataType.Blank => null,
                    XLDataType.Boolean => cell.Value.GetBoolean(),
                    XLDataType.Number => cell.Value.GetNumber(),
                    XLDataType.DateTime => cell.Value.GetDateTime(),
                    XLDataType.TimeSpan => cell.Value.GetTimeSpan().ToString(),
                    _ => cell.Value.ToString()
                };
            }
            table.Rows.Add(arr);
        }
        table.InferTypes();
        return Task.FromResult(table);
    }

    public async Task<(bool, string)> TestAsync(string configJson, CancellationToken ct = default)
    {
        var path = ConnectorConfig.GetString(configJson, "path");
        if (!File.Exists(path)) return (false, $"File not found: {path}");
        try
        {
            var t = await QueryAsync(configJson, null, ct);
            return (true, $"Workbook OK — {t.Columns.Count} columns, {t.RowCount} rows in first sheet.");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }
}

/// <summary>Reads delimited text. Config: { "path": "...", "delimiter": ",", "encoding": "utf-8" }.</summary>
public sealed class CsvConnector : IDataConnector
{
    public string Kind => "csv";

    public async Task<TableData> QueryAsync(string configJson, string? query, CancellationToken ct = default)
    {
        var path = ConnectorConfig.GetString(configJson, "path");
        var delimiter = ConnectorConfig.GetString(configJson, "delimiter", ",");
        var text = await File.ReadAllTextAsync(path, ct);
        return Parse(text, delimiter.Length > 0 ? delimiter[0] : ',');
    }

    public static TableData Parse(string text, char delimiter = ',')
    {
        var table = new TableData();
        var records = ParseRecords(text, delimiter);
        if (records.Count == 0) return table;
        foreach (var name in records[0])
            table.Columns.Add(new ColumnDef { Name = name.Trim() });
        for (var r = 1; r < records.Count; r++)
        {
            var rec = records[r];
            if (rec.Count == 1 && string.IsNullOrWhiteSpace(rec[0])) continue;
            var arr = new object?[table.Columns.Count];
            for (var i = 0; i < table.Columns.Count && i < rec.Count; i++)
                arr[i] = rec[i].Length == 0 ? null : rec[i];
            table.Rows.Add(arr);
        }
        table.InferTypes();
        return table;
    }

    /// <summary>RFC 4180 style parser: quoted fields, escaped quotes, embedded newlines.</summary>
    private static List<List<string>> ParseRecords(string text, char delimiter)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == delimiter) { record.Add(field.ToString()); field.Clear(); }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                record.Add(field.ToString()); field.Clear();
                records.Add(record); record = [];
            }
            else field.Append(c);
        }
        if (field.Length > 0 || record.Count > 0) { record.Add(field.ToString()); records.Add(record); }
        return records;
    }

    public async Task<(bool, string)> TestAsync(string configJson, CancellationToken ct = default)
    {
        var path = ConnectorConfig.GetString(configJson, "path");
        if (!File.Exists(path)) return (false, $"File not found: {path}");
        var t = await QueryAsync(configJson, null, ct);
        return (true, $"CSV OK — {t.Columns.Count} columns, {t.RowCount} rows.");
    }
}

/// <summary>
/// Calls a JSON REST endpoint. Config: { "url", "method", "headers": {..}, "body", "dataPath" }.
/// Query overrides the URL path/query portion when it starts with '/' or 'http'.
/// dataPath is dot-notation to the array in the response (e.g. "data.items").
/// </summary>
public sealed class RestConnector(IHttpClientFactory httpFactory) : IDataConnector
{
    public string Kind => "rest";

    public async Task<TableData> QueryAsync(string configJson, string? query, CancellationToken ct = default)
    {
        var url = ConnectorConfig.GetString(configJson, "url");
        if (!string.IsNullOrWhiteSpace(query))
            url = query.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? query
                : new Uri(new Uri(url), query).ToString();
        var method = ConnectorConfig.GetString(configJson, "method", "GET").ToUpperInvariant();
        var body = ConnectorConfig.GetString(configJson, "body");
        var dataPath = ConnectorConfig.GetString(configJson, "dataPath");

        var client = httpFactory.CreateClient("connector");
        using var req = new HttpRequestMessage(new HttpMethod(method), url);
        foreach (var (k, v) in ConnectorConfig.GetMap(configJson, "headers"))
            req.Headers.TryAddWithoutValidation(k, v);
        if (!string.IsNullOrWhiteSpace(body) && method != "GET")
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var res = await client.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync(ct);
        return JsonToTable(json, dataPath);
    }

    public static TableData JsonToTable(string json, string? dataPath)
    {
        using var doc = JsonDocument.Parse(json);
        var el = doc.RootElement;
        if (!string.IsNullOrWhiteSpace(dataPath))
            foreach (var part in dataPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
                if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(part, out var next)) el = next;
                else throw new InvalidOperationException($"dataPath segment '{part}' not found in response.");

        if (el.ValueKind == JsonValueKind.Object)
        {
            // find the first array property if root is an object and no dataPath given
            foreach (var p in el.EnumerateObject())
                if (p.Value.ValueKind == JsonValueKind.Array) { el = p.Value; break; }
        }
        if (el.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Response does not contain a JSON array. Set 'dataPath' in the connection config.");

        var rows = new List<IDictionary<string, object?>>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
            {
                var d = new Dictionary<string, object?>();
                foreach (var p in item.EnumerateObject())
                    d[p.Name] = TableData.FromJsonElement(p.Value);
                rows.Add(d);
            }
            else rows.Add(new Dictionary<string, object?> { ["value"] = TableData.FromJsonElement(item) });
        }
        return TableData.FromDictionaries(rows);
    }

    public async Task<(bool, string)> TestAsync(string configJson, CancellationToken ct = default)
    {
        try
        {
            var t = await QueryAsync(configJson, null, ct);
            return (true, $"REST OK — {t.Columns.Count} columns, {t.RowCount} rows.");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }
}

/// <summary>POSTs a GraphQL query. Config: { "url", "headers", "dataPath" }. Query = GraphQL document.</summary>
public sealed class GraphQLConnector(IHttpClientFactory httpFactory) : IDataConnector
{
    public string Kind => "graphql";

    public async Task<TableData> QueryAsync(string configJson, string? query, CancellationToken ct = default)
    {
        var url = ConnectorConfig.GetString(configJson, "url");
        var gql = string.IsNullOrWhiteSpace(query) ? ConnectorConfig.GetString(configJson, "query") : query;
        if (string.IsNullOrWhiteSpace(gql)) throw new InvalidOperationException("GraphQL query is required.");
        var dataPath = ConnectorConfig.GetString(configJson, "dataPath");

        var client = httpFactory.CreateClient("connector");
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { query = gql }), Encoding.UTF8, "application/json")
        };
        foreach (var (k, v) in ConnectorConfig.GetMap(configJson, "headers"))
            req.Headers.TryAddWithoutValidation(k, v);

        using var res = await client.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("errors", out var errors))
            throw new InvalidOperationException("GraphQL error: " + errors.GetRawText());
        var path = string.IsNullOrWhiteSpace(dataPath) ? "data" : "data." + dataPath;
        return RestConnector.JsonToTable(json, path);
    }

    public async Task<(bool, string)> TestAsync(string configJson, CancellationToken ct = default)
    {
        try
        {
            var t = await QueryAsync(configJson, ConnectorConfig.GetString(configJson, "query", "{ __typename }"), ct);
            return (true, $"GraphQL OK — {t.RowCount} rows.");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }
}

public sealed class DataConnectorFactory(IEnumerable<IDataConnector> connectors)
{
    private readonly Dictionary<string, IDataConnector> _byKind =
        connectors.ToDictionary(c => c.Kind, StringComparer.OrdinalIgnoreCase);

    public static readonly string[] Kinds = ["sqlite", "sqlserver", "postgresql", "mysql", "oracle", "excel", "csv", "rest", "graphql"];

    public IDataConnector Get(string kind) =>
        _byKind.TryGetValue(kind, out var c) ? c : throw new InvalidOperationException($"Unknown connector kind '{kind}'.");
}

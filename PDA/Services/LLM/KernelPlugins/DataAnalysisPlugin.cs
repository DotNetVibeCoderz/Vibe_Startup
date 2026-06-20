using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Text;
using Microsoft.SemanticKernel;
using PDA.Services.Database;

namespace PDA.Services.LLM.KernelPlugins;

/// <summary>
/// Kernel Plugin: Data analysis - queryToDatabase, getQueryStat.
/// Pre-configured with the target database connection by SemanticKernelFactory.
/// DatabaseName comes from the Connection Name (user-defined), NOT from connection string.
/// </summary>
public class DataAnalysisPlugin
{
    private readonly DatabaseConnectorFactory _dbFactory;
    private readonly ILogger<DataAnalysisPlugin> _logger;

    // Set by SemanticKernelFactory from DatabaseConnection model
    internal string DatabaseType { get; set; } = "SQLite";
    internal string ConnectionString { get; set; } = "Data Source=PDA.db";
    internal string? FilePath { get; set; }
    internal string DatabaseName { get; set; } = "Default";

    public static (int RowCount, bool Truncated, double DurationMs)? LastQueryStats { get; set; }

    public DataAnalysisPlugin(DatabaseConnectorFactory dbFactory, ILogger<DataAnalysisPlugin> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// Execute read-only SQL against the pre-configured database.
    /// DatabaseName displayed in responses comes from the Connection Name, not parsed from connection string.
    /// </summary>
    [KernelFunction("queryToDatabase")]
    [Description("Execute a read-only SQL query (SELECT or WITH/CTE) against the currently connected database. " +
                 "Pass ONLY the SQL — the database is already configured. Returns markdown table (max 100 rows).")]
    public async Task<string> QueryToDatabaseAsync(
        [Description("The SQL SELECT query. Only SELECT and WITH (CTE) are allowed.")] string sql)
    {
        // 🔒 DatabaseName berasal dari Connection Name (user-defined), bukan dari parsing connection string
        var displayName = (string.IsNullOrWhiteSpace(DatabaseName) || DatabaseName == "Default")
            ? $"{DatabaseType} database"
            : DatabaseName;

        var startTime = DateTime.UtcNow;
        try
        {
            var trimmed = sql.Trim().ToUpper();
            if (!trimmed.StartsWith("SELECT") && !trimmed.StartsWith("WITH"))
                return "❌ Error: Only SELECT and WITH (CTE) queries are allowed.";

            _logger.LogInformation("🔍 queryToDatabase → {DbName} ({Type})", displayName, DatabaseType);

            using var connection = _dbFactory.CreateConnection(DatabaseType, ConnectionString, FilePath);
            if (connection is DbConnection dbConn) await dbConn.OpenAsync();
            else await Task.Run(() => connection.Open());

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 60;

            using var reader = await Task.Run(() => command.ExecuteReader());
            var results = new List<Dictionary<string, object?>>();
            while (await Task.Run(() => reader.Read()))
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                results.Add(row);
                if (results.Count >= 100)
                { results.Add(new Dictionary<string, object?> { ["__NOTE__"] = "⚠️ Results truncated at 100 rows." }); break; }
            }
            connection.Close();

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            LastQueryStats = (results.Count(r => !r.ContainsKey("__NOTE__")), results.Count >= 100, duration);

            if (results.Count == 0)
                return $"✅ Query on **{displayName}** in {duration:F0}ms. No results (empty set).";

            var sb = new StringBuilder();
            sb.AppendLine($"✅ **{results.Count} row(s)** from **{displayName}** in {duration:F0}ms\n");
            var columns = results[0].Keys.Where(k => k != "__NOTE__").ToList();
            sb.Append("| "); foreach (var c in columns) sb.Append($"**`{c}`** | "); sb.AppendLine();
            sb.Append("| "); foreach (var _ in columns) sb.Append("--- | "); sb.AppendLine();
            foreach (var row in results.Take(25))
            {
                if (row.ContainsKey("__NOTE__")) { sb.AppendLine($"| {row["__NOTE__"]} |"); break; }
                sb.Append("| ");
                foreach (var c in columns)
                {
                    var v = row.TryGetValue(c, out var val) ? val?.ToString()?.Replace("\n", " ").Replace("|", "\\|") ?? "NULL" : "NULL";
                    if (v.Length > 60) v = v[..60] + "...";
                    sb.Append($"{v} | ");
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Query failed on {Db}", displayName);
            LastQueryStats = null;
            return $"❌ **Query Error on {displayName}:** {ex.Message}";
        }
    }

    /// <summary>Get stats about the last query.</summary>
    [KernelFunction("getQueryStat")]
    [Description("Get statistics about the last query: row count, duration, truncation status.")]
    public string GetQueryStat()
    {
        if (LastQueryStats == null)
            return $"📊 No query executed yet on **{DatabaseName}**.";
        var (cnt, trunc, dur) = LastQueryStats.Value;
        return $"📊 **Last Query on {DatabaseName}:**\n- Rows: **{cnt:N0}**\n- Duration: **{dur:F0}ms**\n- Truncated: {(trunc ? "Yes" : "No")}";
    }
}

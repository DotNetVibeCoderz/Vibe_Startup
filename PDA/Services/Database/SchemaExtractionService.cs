using System.Data;
using System.Data.Common;

namespace PDA.Services.Database;

/// <summary>
/// Extracts database schema information for LLM context
/// </summary>
public class SchemaExtractionService
{
    private readonly DatabaseConnectorFactory _factory;
    private readonly ILogger<SchemaExtractionService> _logger;

    public SchemaExtractionService(DatabaseConnectorFactory factory, ILogger<SchemaExtractionService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<DatabaseSchema> ExtractSchemaAsync(string databaseType, string connectionString, string? filePath = null)
    {
        var schema = new DatabaseSchema();
        try
        {
            using var connection = _factory.CreateConnection(databaseType, connectionString, filePath);
            if (connection is not DbConnection dbConn)
            {
                schema.Error = "Connection does not support schema extraction.";
                return schema;
            }

            await Task.Run(() => dbConn.Open());

            var tables = dbConn.GetSchema("Tables");
            var tableNames = new List<string>();

            foreach (DataRow row in tables.Rows)
            {
                var tableType = row["TABLE_TYPE"]?.ToString();
                if (tableType is "TABLE" or "VIEW" or "BASE TABLE")
                {
                    var tableName = row["TABLE_NAME"]?.ToString();
                    if (!string.IsNullOrEmpty(tableName) && !tableName.StartsWith("sys") && !tableName.StartsWith("sqlite_"))
                        tableNames.Add(tableName);
                }
            }

            foreach (var tableName in tableNames)
            {
                var table = new TableInfo { Name = tableName };
                try
                {
                    var columns = dbConn.GetSchema("Columns", new[] { null, null, tableName, null });
                    foreach (DataRow colRow in columns.Rows)
                    {
                        table.Columns.Add(new ColumnInfo
                        {
                            Name = colRow["COLUMN_NAME"]?.ToString() ?? "",
                            DataType = colRow["DATA_TYPE"]?.ToString() ?? "unknown",
                            IsNullable = colRow["IS_NULLABLE"]?.ToString() == "YES",
                            IsPrimaryKey = table.Columns.Count == 0 && (colRow["COLUMN_NAME"]?.ToString()?.ToLower().EndsWith("id") ?? false)
                        });
                    }
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to get columns for table {Table}", tableName); }
                schema.Tables.Add(table);
            }

            DetectRelationships(schema);
            dbConn.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema extraction failed");
            schema.Error = ex.Message;
        }
        return schema;
    }

    public string GenerateSchemaPrompt(DatabaseSchema schema, string databaseName)
    {
        if (schema.Tables.Count == 0)
            return $"No schema information available for database: {databaseName}";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"## Database: {databaseName}");
        sb.AppendLine($"**Total Tables:** {schema.Tables.Count}");
        sb.AppendLine();

        foreach (var table in schema.Tables)
        {
            sb.AppendLine($"### Table: `{table.Name}`");
            sb.AppendLine("| Column | Type | Nullable | Key |");
            sb.AppendLine("|--------|------|----------|-----|");
            foreach (var col in table.Columns)
            {
                var key = col.IsPrimaryKey ? "PK" : (col.IsForeignKey ? "FK" : "");
                sb.AppendLine($"| `{col.Name}` | {col.DataType} | {(col.IsNullable ? "Yes" : "No")} | {key} |");
            }
            sb.AppendLine();
        }

        if (schema.Relationships.Count > 0)
        {
            sb.AppendLine("### Relationships:");
            foreach (var rel in schema.Relationships)
                sb.AppendLine($"- `{rel.ParentTable}.{rel.ParentColumn}` → `{rel.ChildTable}.{rel.ChildColumn}`");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void DetectRelationships(DatabaseSchema schema)
    {
        foreach (var table in schema.Tables)
        {
            foreach (var col in table.Columns)
            {
                if (col.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && !col.IsPrimaryKey)
                {
                    var referencedTable = col.Name[..^2];
                    var parentTable = schema.Tables.FirstOrDefault(t => t.Name.Equals(referencedTable, StringComparison.OrdinalIgnoreCase));
                    if (parentTable != null)
                    {
                        col.IsForeignKey = true;
                        schema.Relationships.Add(new Relationship { ParentTable = parentTable.Name, ParentColumn = "Id", ChildTable = table.Name, ChildColumn = col.Name });
                    }
                }
            }
        }
    }
}

public class DatabaseSchema
{
    public List<TableInfo> Tables { get; set; } = new();
    public List<Relationship> Relationships { get; set; } = new();
    public string? Error { get; set; }
    public bool HasError => !string.IsNullOrEmpty(Error);
}

public class TableInfo
{
    public string Name { get; set; } = string.Empty;
    public List<ColumnInfo> Columns { get; set; } = new();
}

public class ColumnInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
}

public class Relationship
{
    public string ParentTable { get; set; } = string.Empty;
    public string ParentColumn { get; set; } = string.Empty;
    public string ChildTable { get; set; } = string.Empty;
    public string ChildColumn { get; set; } = string.Empty;
}

namespace BlazorViz.Data;

/// <summary>A configured connection to an external data source.</summary>
public class DataConnection
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>sqlite | sqlserver | postgresql | mysql | oracle | excel | csv | rest | graphql</summary>
    public string Kind { get; set; } = "sqlite";
    /// <summary>JSON config: connectionString / path / url / headers etc.</summary>
    public string ConfigJson { get; set; } = "{}";
    public string? CreatedBy { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>A queryable dataset: connection + query, an uploaded file, or ETL output.</summary>
public class Dataset
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int? ConnectionId { get; set; }
    public DataConnection? Connection { get; set; }
    /// <summary>connection | file | etl</summary>
    public string SourceKind { get; set; } = "connection";
    /// <summary>SQL / REST path / GraphQL query depending on connection kind.</summary>
    public string? Query { get; set; }
    /// <summary>For uploaded files: storage-relative path.</summary>
    public string? FilePath { get; set; }
    public string? SchemaJson { get; set; }
    /// <summary>ETL steps applied after load (JSON array of EtlStep).</summary>
    public string? EtlJson { get; set; }
    /// <summary>csharp | javascript | python</summary>
    public string? ScriptLanguage { get; set; }
    public string? Script { get; set; }
    /// <summary>0 = no auto refresh; otherwise cache TTL / auto-refresh seconds.</summary>
    public int RefreshIntervalSeconds { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastRefreshedUtc { get; set; }
}

public class Dashboard
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Serialized DashboardLayout (tabs, panels, filters).</summary>
    public string LayoutJson { get; set; } = "{}";
    public string? OwnerId { get; set; }
    public string? OwnerName { get; set; }
    public bool IsPublic { get; set; }
    public string? ShareToken { get; set; }
    public int CurrentVersion { get; set; } = 1;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<DashboardVersion> Versions { get; set; } = [];
}

public class DashboardVersion
{
    public int Id { get; set; }
    public int DashboardId { get; set; }
    public int Version { get; set; }
    public string LayoutJson { get; set; } = "{}";
    public string? CreatedBy { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public long Id { get; set; }
    public string? UserName { get; set; }
    public string Category { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>One counted usage event: query | token_in | token_out | chat | api | page.</summary>
public class UsageMetric
{
    public long Id { get; set; }
    public string Kind { get; set; } = "";
    public double Value { get; set; } = 1;
    public string? UserName { get; set; }
    public string? Meta { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public class ChatSession
{
    public int Id { get; set; }
    public string Title { get; set; } = "New chat";
    public string? UserId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<ChatMessageEntity> Messages { get; set; } = [];
}

public class ChatMessageEntity
{
    public long Id { get; set; }
    public int SessionId { get; set; }
    /// <summary>user | assistant | system</summary>
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    /// <summary>JSON array of { name, url, kind }.</summary>
    public string? AttachmentsJson { get; set; }
    public int TokensIn { get; set; }
    public int TokensOut { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public class RagDocument
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string? StoragePath { get; set; }
    /// <summary>pending | indexed | failed</summary>
    public string Status { get; set; } = "pending";
    public string? Error { get; set; }
    public int ChunkCount { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime UploadedUtc { get; set; } = DateTime.UtcNow;
}

public class ApiKey
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Key { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

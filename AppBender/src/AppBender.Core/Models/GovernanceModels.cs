using System.ComponentModel.DataAnnotations;

namespace AppBender.Core.Models;

public class AuditLog
{
    [Key] public long Id { get; set; }
    public string TenantId { get; set; } = "";
    public string? UserId { get; set; }
    public string UserName { get; set; } = "system";
    /// <summary>create | update | delete | publish | rollback | login | execute | export | import</summary>
    public string Action { get; set; } = "";
    /// <summary>entity | record | form | workflow | app | connector | user | setting</summary>
    public string ItemType { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>One usage data point; aggregated by the analytics dashboard.</summary>
public class UsageMetric
{
    [Key] public long Id { get; set; }
    public string TenantId { get; set; } = "";
    /// <summary>page_view | api_query | llm_tokens_in | llm_tokens_out | workflow_run | record_op | response_ms</summary>
    [MaxLength(40)] public string Type { get; set; } = "";
    public double Value { get; set; } = 1;
    public string? Detail { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>Immutable snapshot of a designed item, created on every publish/save-version.</summary>
public class VersionSnapshot
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = "";
    /// <summary>form | workflow | entity | app</summary>
    [MaxLength(20)] public string ItemType { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string ItemName { get; set; } = "";
    public int Version { get; set; }
    public string SnapshotJson { get; set; } = "{}";
    public string? Comment { get; set; }
    public string CreatedBy { get; set; } = "system";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CodeSnippet
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    /// <summary>csharp | javascript | python | sql | json</summary>
    public string Language { get; set; } = "csharp";
    public string Category { get; set; } = "General";
    public string? Description { get; set; }
    public string Code { get; set; } = "";
    public string Tags { get; set; } = "";
    public bool IsBuiltIn { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

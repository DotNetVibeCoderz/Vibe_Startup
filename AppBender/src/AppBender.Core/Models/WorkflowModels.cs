using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AppBender.Core.Common;

namespace AppBender.Core.Models;

public enum TriggerType { Manual, Schedule, Webhook, EntityCreated, EntityUpdated, EntityDeleted, FormSubmitted }

public enum RunStatus { Running, Succeeded, Failed, Cancelled }

/// <summary>A node in a workflow step tree (serialized to WorkflowDefinition.StepsJson).</summary>
public class WorkflowStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    /// <summary>Action key from ActionCatalog (e.g. "http_request", "condition").</summary>
    public string Type { get; set; } = "log";
    public string Name { get; set; } = "";
    public bool Disabled { get; set; }
    /// <summary>Action configuration; values support {{expression}} templates.</summary>
    public Dictionary<string, string> Config { get; set; } = [];
    /// <summary>Nested steps for scope/foreach/do-until containers.</summary>
    public List<WorkflowStep> Children { get; set; } = [];
    /// <summary>Branch executed when a condition evaluates to true.</summary>
    public List<WorkflowStep> TrueSteps { get; set; } = [];
    /// <summary>Branch executed when a condition evaluates to false.</summary>
    public List<WorkflowStep> FalseSteps { get; set; } = [];
    /// <summary>Branches for switch steps, keyed by case value ("default" supported).</summary>
    public Dictionary<string, List<WorkflowStep>> Cases { get; set; } = [];

    public string Cfg(string key, string fallback = "") =>
        Config.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : fallback;
}

public class WorkflowDefinition
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Icon { get; set; } = "⚡";
    public TriggerType TriggerType { get; set; } = TriggerType.Manual;
    /// <summary>Trigger settings: cron, webhookKey, entityName, formId...</summary>
    public string TriggerConfigJson { get; set; } = "{}";
    public string StepsJson { get; set; } = "[]";
    public bool IsEnabled { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public List<WorkflowStep> Steps
    {
        get => JsonUtil.DeserializeOrNew<List<WorkflowStep>>(StepsJson);
        set => StepsJson = JsonUtil.Serialize(value);
    }

    [NotMapped]
    public Dictionary<string, string> TriggerConfig
    {
        get => JsonUtil.DeserializeOrNew<Dictionary<string, string>>(TriggerConfigJson);
        set => TriggerConfigJson = JsonUtil.Serialize(value);
    }
}

public class StepLog
{
    public string StepId { get; set; } = "";
    public string StepName { get; set; } = "";
    public string StepType { get; set; } = "";
    public string Status { get; set; } = "succeeded";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public long DurationMs { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
}

public class WorkflowRun
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = "";
    public string WorkflowId { get; set; } = "";
    public string WorkflowName { get; set; } = "";
    public RunStatus Status { get; set; } = RunStatus.Running;
    public string TriggeredBy { get; set; } = "manual";
    public string InputJson { get; set; } = "{}";
    public string? OutputJson { get; set; }
    public string LogJson { get; set; } = "[]";
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }

    [NotMapped]
    public List<StepLog> Logs
    {
        get => JsonUtil.DeserializeOrNew<List<StepLog>>(LogJson);
        set => LogJson = JsonUtil.Serialize(value);
    }
}

using BlazePoint.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazePoint.Services;

public class WfNode
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("type")] public string Type { get; set; } = "Start"; // Start | Approval | Condition | Notify | End
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("x")] public double X { get; set; }
    [JsonPropertyName("y")] public double Y { get; set; }
    [JsonPropertyName("assignRole")] public string? AssignRole { get; set; }
    [JsonPropertyName("assignTo")] public string? AssignTo { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("conditionKey")] public string? ConditionKey { get; set; }
    [JsonPropertyName("conditionValue")] public string? ConditionValue { get; set; }
}

public class WfEdge
{
    [JsonPropertyName("from")] public string From { get; set; } = "";
    [JsonPropertyName("to")] public string To { get; set; } = "";
    [JsonPropertyName("label")] public string? Label { get; set; } // for Condition: "yes"/"no"
}

public class WfGraph
{
    [JsonPropertyName("nodes")] public List<WfNode> Nodes { get; set; } = [];
    [JsonPropertyName("edges")] public List<WfEdge> Edges { get; set; } = [];
}

public class WorkflowService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    NotificationService notifications,
    AuditService audit)
{
    public static WfGraph ParseGraph(string json)
    {
        try { return JsonSerializer.Deserialize<WfGraph>(json) ?? new(); }
        catch { return new(); }
    }

    // ---------- Definitions ----------
    public async Task<List<WorkflowDefinition>> GetDefinitionsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.WorkflowDefinitions.OrderBy(d => d.Name).ToListAsync();
    }

    public async Task<WorkflowDefinition?> GetDefinitionAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.WorkflowDefinitions.FindAsync(id);
    }

    public async Task<WorkflowDefinition> SaveDefinitionAsync(WorkflowDefinition def, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (def.Id == 0) db.WorkflowDefinitions.Add(def);
        else db.WorkflowDefinitions.Update(def);
        await db.SaveChangesAsync();
        await audit.LogAsync("Workflow", $"Simpan workflow '{def.Name}'", userId);
        return def;
    }

    public async Task DeleteDefinitionAsync(int id, string? userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var def = await db.WorkflowDefinitions.FindAsync(id);
        if (def is null) return;
        db.WorkflowDefinitions.Remove(def);
        await db.SaveChangesAsync();
        await audit.LogAsync("Workflow", $"Hapus workflow '{def.Name}'", userId);
    }

    // ---------- Instances ----------
    public async Task<List<WorkflowInstance>> GetInstancesAsync(int? definitionId = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.WorkflowInstances.Include(i => i.Definition).Include(i => i.Tasks).AsQueryable();
        if (definitionId.HasValue) q = q.Where(i => i.DefinitionId == definitionId);
        return await q.OrderByDescending(i => i.StartedAt).ToListAsync();
    }

    public async Task<List<ApprovalTask>> GetMyTasksAsync(string userId, IList<string> roles)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        // Tasks assigned directly to the user, or to a role the user holds (AssignedToId = "role:X")
        var roleKeys = roles.Select(r => $"role:{r}").ToList();
        return await db.ApprovalTasks
            .Include(t => t.Instance).ThenInclude(i => i!.Definition)
            .Where(t => t.Status == ApprovalStatus.Pending &&
                        (t.AssignedToId == userId || roleKeys.Contains(t.AssignedToId!)))
            .OrderBy(t => t.CreatedAt).ToListAsync();
    }

    public async Task<WorkflowInstance> StartAsync(int definitionId, string subject, string? userId, Dictionary<string, string>? context = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var def = await db.WorkflowDefinitions.FirstAsync(d => d.Id == definitionId);
        var graph = ParseGraph(def.DefinitionJson);
        var start = graph.Nodes.FirstOrDefault(n => n.Type == "Start")
            ?? throw new InvalidOperationException("Workflow tidak punya node Start.");

        var instance = new WorkflowInstance
        {
            DefinitionId = definitionId, Subject = subject, StartedById = userId,
            CurrentNodeId = start.Id,
            ContextJson = JsonSerializer.Serialize(context ?? [])
        };
        db.WorkflowInstances.Add(instance);
        await db.SaveChangesAsync();
        await audit.LogAsync("Workflow", $"Mulai workflow '{def.Name}': {subject}", userId);

        await AdvanceAsync(db, instance, graph, start.Id);
        await db.SaveChangesAsync();
        return instance;
    }

    public async Task CompleteTaskAsync(int taskId, bool approved, string comment, string userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var task = await db.ApprovalTasks.Include(t => t.Instance).ThenInclude(i => i!.Definition)
            .FirstAsync(t => t.Id == taskId);
        task.Status = approved ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
        task.Comment = comment;
        task.CompletedAt = DateTime.UtcNow;

        var instance = task.Instance!;
        var graph = ParseGraph(instance.Definition!.DefinitionJson);

        if (!approved)
        {
            // follow "no" edge if the designer defined one, otherwise reject the whole instance
            var noEdge = graph.Edges.FirstOrDefault(e => e.From == task.NodeId &&
                string.Equals(e.Label, "no", StringComparison.OrdinalIgnoreCase));
            if (noEdge is null)
            {
                instance.Status = WorkflowStatus.Rejected;
                instance.CompletedAt = DateTime.UtcNow;
                if (instance.StartedById is not null)
                    await notifications.NotifyAsync(instance.StartedById, "Workflow ditolak",
                        $"'{instance.Subject}' ditolak: {comment}", "/workflows");
            }
            else
            {
                await AdvanceAsync(db, instance, graph, task.NodeId, forcedNext: noEdge.To);
            }
        }
        else
        {
            await AdvanceAsync(db, instance, graph, task.NodeId);
        }
        await db.SaveChangesAsync();
        await audit.LogAsync("Workflow", $"Task '{task.Title}' {(approved ? "disetujui" : "ditolak")}", userId);
    }

    /// <summary>Walk the graph from a node until we hit an Approval (wait) or End.</summary>
    private async Task AdvanceAsync(ApplicationDbContext db, WorkflowInstance instance, WfGraph graph,
        string fromNodeId, string? forcedNext = null)
    {
        var context = JsonSerializer.Deserialize<Dictionary<string, string>>(instance.ContextJson) ?? [];
        var currentId = forcedNext ?? NextNode(graph, fromNodeId, context);

        while (currentId is not null)
        {
            var node = graph.Nodes.FirstOrDefault(n => n.Id == currentId);
            if (node is null) break;
            instance.CurrentNodeId = node.Id;

            switch (node.Type)
            {
                case "End":
                    instance.Status = WorkflowStatus.Completed;
                    instance.CompletedAt = DateTime.UtcNow;
                    if (instance.StartedById is not null)
                        await notifications.NotifyAsync(instance.StartedById, "Workflow selesai",
                            $"'{instance.Subject}' telah selesai.", "/workflows");
                    return;

                case "Approval":
                    var assignee = !string.IsNullOrEmpty(node.AssignTo) ? node.AssignTo
                        : !string.IsNullOrEmpty(node.AssignRole) ? $"role:{node.AssignRole}" : "role:Admin";
                    db.ApprovalTasks.Add(new ApprovalTask
                    {
                        InstanceId = instance.Id, NodeId = node.Id,
                        Title = $"{node.Label}: {instance.Subject}", AssignedToId = assignee
                    });
                    if (assignee.StartsWith("role:"))
                        await notifications.NotifyRoleAsync(assignee[5..], "Persetujuan dibutuhkan",
                            $"{node.Label}: {instance.Subject}", "/workflows/tasks");
                    else
                        await notifications.NotifyAsync(assignee, "Persetujuan dibutuhkan",
                            $"{node.Label}: {instance.Subject}", "/workflows/tasks");
                    return; // wait for approval

                case "Notify":
                    if (instance.StartedById is not null)
                        await notifications.NotifyAsync(instance.StartedById, node.Label,
                            node.Message ?? $"Update workflow: {instance.Subject}", "/workflows");
                    break;

                case "Condition":
                case "Start":
                    break; // pass-through, next edge decides
            }
            currentId = NextNode(graph, node.Id, context);
        }
    }

    private static string? NextNode(WfGraph graph, string fromId, Dictionary<string, string> context)
    {
        var node = graph.Nodes.FirstOrDefault(n => n.Id == fromId);
        var edges = graph.Edges.Where(e => e.From == fromId).ToList();
        if (edges.Count == 0) return null;

        if (node?.Type == "Condition" && node.ConditionKey is not null)
        {
            var matches = context.TryGetValue(node.ConditionKey, out var v) &&
                          string.Equals(v, node.ConditionValue, StringComparison.OrdinalIgnoreCase);
            var target = edges.FirstOrDefault(e =>
                string.Equals(e.Label, matches ? "yes" : "no", StringComparison.OrdinalIgnoreCase));
            return (target ?? edges[0]).To;
        }
        // default: first edge without special label
        return (edges.FirstOrDefault(e => string.IsNullOrEmpty(e.Label) ||
                e.Label.Equals("yes", StringComparison.OrdinalIgnoreCase)) ?? edges[0]).To;
    }
}

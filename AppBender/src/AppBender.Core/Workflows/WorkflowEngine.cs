using System.Diagnostics;
using AppBender.Core.Common;
using AppBender.Core.Data;
using AppBender.Core.Models;
using AppBender.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AppBender.Core.Workflows;

/// <summary>Mutable state shared by all steps of one workflow run.</summary>
public class WorkflowContext
{
    public required WorkflowDefinition Workflow { get; init; }
    public Dictionary<string, object?> Root { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> Vars { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> StepOutputs { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<StepLog> Logs { get; } = [];
    /// <summary>Set by the "respond" action; returned to webhook callers.</summary>
    public object? Response { get; set; }
    public CancellationToken CancellationToken { get; init; }
    public int ExecutedSteps { get; set; }
    public const int MaxSteps = 1000;

    public static WorkflowContext Create(WorkflowDefinition workflow, object? triggerData, CancellationToken ct)
    {
        var ctx = new WorkflowContext { Workflow = workflow, CancellationToken = ct };
        ctx.Root["trigger"] = triggerData;
        ctx.Root["vars"] = ctx.Vars;
        ctx.Root["steps"] = ctx.StepOutputs;
        ctx.Root["workflow"] = new Dictionary<string, object?> { ["id"] = workflow.Id, ["name"] = workflow.Name };
        return ctx;
    }

    public string Render(string? template) => TemplateEngine.Render(template, Root);
    public object? Eval(string? template) => TemplateEngine.EvaluateValue(template, Root);

    /// <summary>Parses a rendered JSON config value into CLR objects; falls back to the rendered string.</summary>
    public object? RenderJson(string? template)
    {
        var rendered = Render(template);
        if (string.IsNullOrWhiteSpace(rendered)) return null;
        try { return JsonUtil.ToClr(System.Text.Json.JsonDocument.Parse(rendered).RootElement); }
        catch { return rendered; }
    }

    public Dictionary<string, object?> RenderJsonObject(string? template)
        => RenderJson(template) as Dictionary<string, object?> ?? [];
}

/// <summary>Implement one workflow action type. Registered in DI; discovered by type key.</summary>
public interface IWorkflowAction
{
    string Type { get; }
    Task<object?> ExecuteAsync(WorkflowStep step, WorkflowContext context);
}

public class TerminateWorkflowException(RunStatus status) : Exception($"Workflow terminated: {status}")
{
    public RunStatus Status { get; } = status;
}

/// <summary>Executes a workflow definition step tree.</summary>
public class WorkflowEngine(IEnumerable<IWorkflowAction> actions, ILogger<WorkflowEngine> logger)
{
    private readonly Dictionary<string, IWorkflowAction> _actions =
        actions.ToDictionary(a => a.Type, StringComparer.OrdinalIgnoreCase);

    public async Task<WorkflowContext> ExecuteAsync(WorkflowDefinition workflow, object? triggerData, CancellationToken ct = default)
    {
        var ctx = WorkflowContext.Create(workflow, triggerData, ct);
        try
        {
            await ExecuteStepsAsync(workflow.Steps, ctx);
        }
        catch (TerminateWorkflowException tex) when (tex.Status == RunStatus.Succeeded)
        {
            // graceful stop
        }
        return ctx;
    }

    public async Task ExecuteStepsAsync(List<WorkflowStep> steps, WorkflowContext ctx)
    {
        foreach (var step in steps)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            if (step.Disabled) continue;
            if (++ctx.ExecutedSteps > WorkflowContext.MaxSteps)
                throw new InvalidOperationException($"Workflow exceeded {WorkflowContext.MaxSteps} steps.");

            var log = new StepLog { StepId = step.Id, StepName = step.Name, StepType = step.Type };
            var sw = Stopwatch.StartNew();
            try
            {
                var output = await ExecuteStepAsync(step, ctx);
                sw.Stop();
                log.DurationMs = sw.ElapsedMilliseconds;
                if (output is not null)
                {
                    var summary = TemplateEngine.ToText(output);
                    log.Output = summary.Length > 2000 ? summary[..2000] + "…" : summary;
                }
                ctx.Logs.Add(log);
                Store(step, ctx, output);
            }
            catch (TerminateWorkflowException) { ctx.Logs.Add(log); throw; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                sw.Stop();
                log.DurationMs = sw.ElapsedMilliseconds;
                log.Status = "failed";
                log.Error = ex.Message;
                ctx.Logs.Add(log);
                logger.LogWarning(ex, "Workflow {Workflow} step {Step} failed", ctx.Workflow.Name, step.Name);
                throw new InvalidOperationException($"Step '{(string.IsNullOrEmpty(step.Name) ? step.Type : step.Name)}' failed: {ex.Message}", ex);
            }
        }
    }

    private static void Store(WorkflowStep step, WorkflowContext ctx, object? output)
    {
        var wrapped = new Dictionary<string, object?> { ["output"] = output };
        ctx.StepOutputs[step.Id] = wrapped;
        if (!string.IsNullOrWhiteSpace(step.Name))
            ctx.StepOutputs[DataHubService.Slugify(step.Name)] = wrapped;
    }

    private async Task<object?> ExecuteStepAsync(WorkflowStep step, WorkflowContext ctx)
    {
        switch (step.Type)
        {
            case "condition":
            {
                var result = EvaluateCondition(step, ctx);
                await ExecuteStepsAsync(result ? step.TrueSteps : step.FalseSteps, ctx);
                return result;
            }
            case "switch":
            {
                var value = ctx.Render(step.Cfg("on"));
                var branch = step.Cases.FirstOrDefault(c =>
                    string.Equals(c.Key, value, StringComparison.OrdinalIgnoreCase)).Value;
                branch ??= step.Cases.TryGetValue("default", out var def) ? def : null;
                if (branch is not null) await ExecuteStepsAsync(branch, ctx);
                return value;
            }
            case "foreach":
            {
                var items = ctx.Eval(step.Cfg("items")) switch
                {
                    IList<object?> list => list,
                    System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } je =>
                        je.EnumerateArray().Select(e => JsonUtil.ToClr(e)).ToList(),
                    string s when !string.IsNullOrWhiteSpace(s) => ParseListOrSplit(s),
                    null => [],
                    var single => [single]
                };
                var index = 0;
                foreach (var item in items)
                {
                    ctx.Vars["item"] = item is System.Text.Json.JsonElement ije ? JsonUtil.ToClr(ije) : item;
                    ctx.Vars["index"] = index++;
                    await ExecuteStepsAsync(step.Children, ctx);
                }
                return items.Count;
            }
            case "do_until":
            {
                var max = int.TryParse(step.Cfg("maxIterations", "100"), out var m) ? Math.Clamp(m, 1, 10_000) : 100;
                var iterations = 0;
                do
                {
                    await ExecuteStepsAsync(step.Children, ctx);
                    iterations++;
                } while (!EvaluateCondition(step, ctx) && iterations < max);
                return iterations;
            }
            case "scope":
                await ExecuteStepsAsync(step.Children, ctx);
                return null;
            case "delay":
            {
                var seconds = double.TryParse(ctx.Render(step.Cfg("seconds", "1")), out var s) ? Math.Clamp(s, 0, 3600) : 1;
                await Task.Delay(TimeSpan.FromSeconds(seconds), ctx.CancellationToken);
                return seconds;
            }
            case "terminate":
            {
                var status = step.Cfg("status", "succeeded") switch
                {
                    "failed" => RunStatus.Failed,
                    "cancelled" => RunStatus.Cancelled,
                    _ => RunStatus.Succeeded
                };
                if (status == RunStatus.Succeeded) throw new TerminateWorkflowException(status);
                throw new InvalidOperationException($"Workflow terminated with status '{status}'.");
            }
            default:
            {
                if (!_actions.TryGetValue(step.Type, out var action))
                    throw new InvalidOperationException($"Unknown action type '{step.Type}'.");
                return await action.ExecuteAsync(step, ctx);
            }
        }
    }

    private static List<object?> ParseListOrSplit(string s)
    {
        try
        {
            var parsed = JsonUtil.ToClr(System.Text.Json.JsonDocument.Parse(s).RootElement);
            if (parsed is List<object?> list) return list;
        }
        catch { }
        return s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => (object?)x).ToList();
    }

    private static bool EvaluateCondition(WorkflowStep step, WorkflowContext ctx)
    {
        var left = ctx.Render(step.Cfg("left"));
        var right = ctx.Render(step.Cfg("right"));
        var op = step.Cfg("op", "eq");
        var leftIsNum = double.TryParse(left, out var l);
        var rightIsNum = double.TryParse(right, out var r);
        var numeric = leftIsNum && rightIsNum;
        return op switch
        {
            "eq" => numeric ? l == r : string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            "neq" => numeric ? l != r : !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            "gt" => numeric && l > r,
            "gte" => numeric && l >= r,
            "lt" => numeric && l < r,
            "lte" => numeric && l <= r,
            "contains" => left.Contains(right, StringComparison.OrdinalIgnoreCase),
            "startswith" => left.StartsWith(right, StringComparison.OrdinalIgnoreCase),
            "empty" => string.IsNullOrWhiteSpace(left),
            "notempty" => !string.IsNullOrWhiteSpace(left),
            "true" => left.Equals("true", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}

/// <summary>
/// Runs workflows in an isolated DI scope (so triggers fired from background services
/// get correctly-tenanted scoped services) and persists the WorkflowRun record.
/// </summary>
public class WorkflowRunner(IServiceScopeFactory scopeFactory, ILogger<WorkflowRunner> logger)
{
    public async Task<WorkflowRun> RunAsync(
        WorkflowDefinition workflow, object? triggerData, string triggeredBy,
        CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(workflow.TenantId, null, $"workflow:{workflow.Name}");

        var run = new WorkflowRun
        {
            TenantId = workflow.TenantId,
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            TriggeredBy = triggeredBy,
            InputJson = JsonUtil.Serialize(triggerData)
        };

        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using (var db = await dbFactory.CreateDbContextAsync(ct))
        {
            db.WorkflowRuns.Add(run);
            await db.SaveChangesAsync(ct);
        }

        var engine = scope.ServiceProvider.GetRequiredService<WorkflowEngine>();
        WorkflowContext? ctx = null;
        try
        {
            ctx = await engine.ExecuteAsync(workflow, triggerData, ct);
            run.Status = RunStatus.Succeeded;
            run.OutputJson = JsonUtil.Serialize(ctx.Response ?? ctx.Vars);
        }
        catch (OperationCanceledException)
        {
            run.Status = RunStatus.Cancelled;
        }
        catch (Exception ex)
        {
            run.Status = RunStatus.Failed;
            run.Error = ex.Message;
            logger.LogWarning(ex, "Workflow {Name} run failed", workflow.Name);
        }
        finally
        {
            run.FinishedAt = DateTime.UtcNow;
            if (ctx is not null) run.Logs = ctx.Logs;
            try
            {
                await using var db = await dbFactory.CreateDbContextAsync(CancellationToken.None);
                db.WorkflowRuns.Update(run);
                await db.SaveChangesAsync(CancellationToken.None);
                var usage = scope.ServiceProvider.GetRequiredService<IUsageService>();
                usage.TrackFireAndForget("workflow_run", 1, workflow.Name);
            }
            catch (Exception persistEx)
            {
                logger.LogError(persistEx, "Failed persisting workflow run {Id}", run.Id);
            }
        }
        if (ctx is not null) run.Logs = ctx.Logs;
        return run;
    }
}

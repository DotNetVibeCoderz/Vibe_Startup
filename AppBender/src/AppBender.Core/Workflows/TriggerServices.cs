using AppBender.Core.Data;
using AppBender.Core.Models;
using AppBender.Core.Services;
using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppBender.Core.Workflows;

/// <summary>
/// Fires Schedule-triggered workflows using cron expressions
/// (TriggerConfig["cron"], 5-field cron, UTC). Checks once per 20 seconds.
/// </summary>
public class ScheduleTriggerService(
    IServiceScopeFactory scopeFactory,
    WorkflowRunner runner,
    ILogger<ScheduleTriggerService> logger) : BackgroundService
{
    private readonly Dictionary<string, DateTime> _lastRuns = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // small startup delay so seeding finishes first
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Schedule trigger tick failed"); }
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        List<WorkflowDefinition> scheduled;
        using (var scope = scopeFactory.CreateScope())
        {
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            scheduled = await db.WorkflowDefinitions.AsNoTracking()
                .Where(w => w.IsEnabled && w.TriggerType == TriggerType.Schedule)
                .ToListAsync(ct);
        }

        var now = DateTime.UtcNow;
        foreach (var workflow in scheduled)
        {
            var cronText = workflow.TriggerConfig.GetValueOrDefault("cron");
            if (string.IsNullOrWhiteSpace(cronText)) continue;

            CronExpression cron;
            try { cron = CronExpression.Parse(cronText); }
            catch { continue; }

            var last = _lastRuns.TryGetValue(workflow.Id, out var l) ? l : now.AddSeconds(-25);
            var next = cron.GetNextOccurrence(last, TimeZoneInfo.Utc);
            if (next is null || next > now) continue;

            _lastRuns[workflow.Id] = now;
            var trigger = new Dictionary<string, object?>
            {
                ["type"] = "schedule",
                ["scheduledAt"] = next.Value.ToString("O"),
                ["firedAt"] = now.ToString("O")
            };
            _ = Task.Run(() => runner.RunAsync(workflow, trigger, "schedule", ct), ct);
        }
    }
}

/// <summary>Fires EntityCreated/Updated/Deleted and FormSubmitted workflows from the in-process event bus.</summary>
public class EventTriggerService(
    IServiceScopeFactory scopeFactory,
    EventBus events,
    WorkflowRunner runner,
    ILogger<EventTriggerService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        events.RecordChanged += OnRecordChangedAsync;
        events.FormSubmitted += OnFormSubmittedAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        events.RecordChanged -= OnRecordChangedAsync;
        events.FormSubmitted -= OnFormSubmittedAsync;
        return Task.CompletedTask;
    }

    private async Task OnRecordChangedAsync(RecordChangedEvent evt)
    {
        var triggerType = evt.ChangeType switch
        {
            "created" => TriggerType.EntityCreated,
            "updated" => TriggerType.EntityUpdated,
            _ => TriggerType.EntityDeleted
        };
        var workflows = await FindAsync(evt.TenantId, triggerType);
        foreach (var workflow in workflows)
        {
            var entity = workflow.TriggerConfig.GetValueOrDefault("entityName");
            if (!string.IsNullOrEmpty(entity) && !string.Equals(entity, evt.EntityName, StringComparison.OrdinalIgnoreCase))
                continue;
            var record = evt.Record.Data;
            record["id"] = evt.Record.Id;
            var trigger = new Dictionary<string, object?>
            {
                ["type"] = evt.ChangeType,
                ["entity"] = evt.EntityName,
                ["record"] = record
            };
            _ = Task.Run(() => runner.RunAsync(workflow, trigger, $"entity:{evt.EntityName}:{evt.ChangeType}"));
        }
    }

    private async Task OnFormSubmittedAsync(FormSubmittedEvent evt)
    {
        var workflows = await FindAsync(evt.TenantId, TriggerType.FormSubmitted);
        foreach (var workflow in workflows)
        {
            var formId = workflow.TriggerConfig.GetValueOrDefault("formId");
            if (!string.IsNullOrEmpty(formId) && formId != evt.FormId) continue;
            var trigger = new Dictionary<string, object?>
            {
                ["type"] = "form_submitted",
                ["formId"] = evt.FormId,
                ["formName"] = evt.FormName,
                ["recordId"] = evt.RecordId,
                ["values"] = evt.Values
            };
            _ = Task.Run(() => runner.RunAsync(workflow, trigger, $"form:{evt.FormName}"));
        }
    }

    private async Task<List<WorkflowDefinition>> FindAsync(string tenantId, TriggerType type)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
            await using var db = await dbFactory.CreateDbContextAsync();
            return await db.WorkflowDefinitions.AsNoTracking()
                .Where(w => w.IsEnabled && w.TenantId == tenantId && w.TriggerType == type)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed loading {Type} trigger workflows", type);
            return [];
        }
    }
}

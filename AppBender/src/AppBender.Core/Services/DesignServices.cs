using AppBender.Core.Common;
using AppBender.Core.Data;
using AppBender.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace AppBender.Core.Services;

// ---------------------------------------------------------------- Forms

public interface IFormService
{
    Task<List<FormDefinition>> GetAllAsync();
    Task<FormDefinition?> GetAsync(string id);
    Task<FormDefinition?> GetBySlugAsync(string slug);
    Task<FormDefinition> SaveAsync(FormDefinition form, string? comment = null);
    Task DeleteAsync(string id);
    Task<string> SubmitAsync(FormDefinition form, Dictionary<string, object?> values);
}

public class FormService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ITenantContext tenant,
    IDataHubService dataHub,
    IAuditService audit,
    IVersionService versions,
    EventBus events) : IFormService
{
    public async Task<List<FormDefinition>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.FormDefinitions.AsNoTracking()
            .Where(f => f.TenantId == tenant.TenantId).OrderBy(f => f.Name).ToListAsync();
    }

    public async Task<FormDefinition?> GetAsync(string id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.FormDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(f => f.TenantId == tenant.TenantId && f.Id == id);
    }

    public async Task<FormDefinition?> GetBySlugAsync(string slug)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.FormDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(f => f.TenantId == tenant.TenantId && f.Slug == slug);
    }

    public async Task<FormDefinition> SaveAsync(FormDefinition form, string? comment = null)
    {
        if (string.IsNullOrWhiteSpace(form.Name)) throw new InvalidOperationException("Form name is required.");
        if (string.IsNullOrWhiteSpace(form.Slug)) form.Slug = DataHubService.Slugify(form.Name);

        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.FormDefinitions.FirstOrDefaultAsync(f => f.TenantId == tenant.TenantId && f.Id == form.Id);
        if (existing is null)
        {
            form.TenantId = tenant.TenantId;
            form.Version = 1;
            db.FormDefinitions.Add(form);
            await db.SaveChangesAsync();
            await audit.LogAsync("create", "form", form.Id, form.Name);
        }
        else
        {
            existing.Name = form.Name;
            existing.Slug = form.Slug;
            existing.Description = form.Description;
            existing.Icon = form.Icon;
            existing.EntityName = form.EntityName;
            existing.LayoutJson = form.LayoutJson;
            existing.SubmitWorkflowId = form.SubmitWorkflowId;
            existing.SubmitLabel = form.SubmitLabel;
            existing.IsPublished = form.IsPublished;
            existing.Version++;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            form = existing;
            await audit.LogAsync("update", "form", form.Id, form.Name);
        }
        await versions.SnapshotAsync("form", form.Id, form.Name, form.Version, JsonUtil.Serialize(form), comment);
        return form;
    }

    public async Task DeleteAsync(string id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var form = await db.FormDefinitions.FirstOrDefaultAsync(f => f.TenantId == tenant.TenantId && f.Id == id);
        if (form is null) return;
        db.FormDefinitions.Remove(form);
        await db.SaveChangesAsync();
        await audit.LogAsync("delete", "form", id, form.Name);
    }

    /// <summary>Stores the submission (when entity-bound) and raises the FormSubmitted event.</summary>
    public async Task<string> SubmitAsync(FormDefinition form, Dictionary<string, object?> values)
    {
        string? recordId = null;
        if (!string.IsNullOrEmpty(form.EntityName))
        {
            var record = await dataHub.CreateRecordAsync(form.EntityName, values);
            recordId = record.Id;
        }
        await events.PublishAsync(new FormSubmittedEvent(tenant.TenantId, form.Id, form.Name, values, recordId));
        await audit.LogAsync("submit", "form", form.Id, form.Name);
        return recordId ?? "";
    }
}

// ---------------------------------------------------------------- Workflows

public interface IWorkflowService
{
    Task<List<WorkflowDefinition>> GetAllAsync();
    Task<WorkflowDefinition?> GetAsync(string id);
    Task<WorkflowDefinition> SaveAsync(WorkflowDefinition workflow, string? comment = null);
    Task DeleteAsync(string id);
    Task<List<WorkflowRun>> GetRunsAsync(string? workflowId = null, int count = 100);
    Task<WorkflowRun?> GetRunAsync(string runId);
}

public class WorkflowService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ITenantContext tenant,
    IAuditService audit,
    IVersionService versions) : IWorkflowService
{
    public async Task<List<WorkflowDefinition>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.WorkflowDefinitions.AsNoTracking()
            .Where(w => w.TenantId == tenant.TenantId).OrderBy(w => w.Name).ToListAsync();
    }

    public async Task<WorkflowDefinition?> GetAsync(string id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.WorkflowDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(w => w.TenantId == tenant.TenantId && w.Id == id);
    }

    public async Task<WorkflowDefinition> SaveAsync(WorkflowDefinition workflow, string? comment = null)
    {
        if (string.IsNullOrWhiteSpace(workflow.Name)) throw new InvalidOperationException("Workflow name is required.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.WorkflowDefinitions.FirstOrDefaultAsync(w => w.TenantId == tenant.TenantId && w.Id == workflow.Id);
        if (existing is null)
        {
            workflow.TenantId = tenant.TenantId;
            workflow.Version = 1;
            db.WorkflowDefinitions.Add(workflow);
            await db.SaveChangesAsync();
            await audit.LogAsync("create", "workflow", workflow.Id, workflow.Name);
        }
        else
        {
            existing.Name = workflow.Name;
            existing.Description = workflow.Description;
            existing.Icon = workflow.Icon;
            existing.TriggerType = workflow.TriggerType;
            existing.TriggerConfigJson = workflow.TriggerConfigJson;
            existing.StepsJson = workflow.StepsJson;
            existing.IsEnabled = workflow.IsEnabled;
            existing.Version++;
            existing.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            workflow = existing;
            await audit.LogAsync("update", "workflow", workflow.Id, workflow.Name);
        }
        await versions.SnapshotAsync("workflow", workflow.Id, workflow.Name, workflow.Version, JsonUtil.Serialize(workflow), comment);
        return workflow;
    }

    public async Task DeleteAsync(string id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var wf = await db.WorkflowDefinitions.FirstOrDefaultAsync(w => w.TenantId == tenant.TenantId && w.Id == id);
        if (wf is null) return;
        db.WorkflowDefinitions.Remove(wf);
        await db.SaveChangesAsync();
        await audit.LogAsync("delete", "workflow", id, wf.Name);
    }

    public async Task<List<WorkflowRun>> GetRunsAsync(string? workflowId = null, int count = 100)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.WorkflowRuns.AsNoTracking().Where(r => r.TenantId == tenant.TenantId);
        if (!string.IsNullOrEmpty(workflowId)) q = q.Where(r => r.WorkflowId == workflowId);
        return await q.OrderByDescending(r => r.StartedAt).Take(count).ToListAsync();
    }

    public async Task<WorkflowRun?> GetRunAsync(string runId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.WorkflowRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId);
    }
}

// ---------------------------------------------------------------- Apps

public interface IAppService
{
    Task<List<AppDefinition>> GetAllAsync();
    Task<AppDefinition?> GetAsync(string id);
    Task<AppDefinition?> GetBySlugAsync(string slug);
    Task<AppDefinition> SaveAsync(AppDefinition app);
    Task<AppDefinition> PublishAsync(string id, bool publish);
    Task DeleteAsync(string id);
}

public class AppService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ITenantContext tenant,
    IAuditService audit,
    IVersionService versions) : IAppService
{
    public async Task<List<AppDefinition>> GetAllAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.AppDefinitions.AsNoTracking()
            .Where(a => a.TenantId == tenant.TenantId).OrderBy(a => a.Name).ToListAsync();
    }

    public async Task<AppDefinition?> GetAsync(string id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.AppDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenant.TenantId && a.Id == id);
    }

    /// <summary>Slug lookup is global (public app URLs are cross-tenant unique).</summary>
    public async Task<AppDefinition?> GetBySlugAsync(string slug)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.AppDefinitions.AsNoTracking().FirstOrDefaultAsync(a => a.Slug == slug);
    }

    public async Task<AppDefinition> SaveAsync(AppDefinition app)
    {
        if (string.IsNullOrWhiteSpace(app.Name)) throw new InvalidOperationException("App name is required.");
        await using var db = await dbFactory.CreateDbContextAsync();

        if (string.IsNullOrWhiteSpace(app.Slug))
        {
            var baseSlug = DataHubService.Slugify(app.Name);
            var slug = baseSlug;
            var i = 1;
            while (await db.AppDefinitions.AnyAsync(a => a.Slug == slug && a.Id != app.Id))
                slug = $"{baseSlug}-{++i}";
            app.Slug = slug;
        }

        var existing = await db.AppDefinitions.FirstOrDefaultAsync(a => a.TenantId == tenant.TenantId && a.Id == app.Id);
        if (existing is null)
        {
            app.TenantId = tenant.TenantId;
            db.AppDefinitions.Add(app);
            await db.SaveChangesAsync();
            await audit.LogAsync("create", "app", app.Id, app.Name);
            return app;
        }

        existing.Name = app.Name;
        existing.Slug = app.Slug;
        existing.Description = app.Description;
        existing.Icon = app.Icon;
        existing.Color = app.Color;
        existing.HomeFormId = app.HomeFormId;
        existing.FormIdsJson = app.FormIdsJson;
        existing.WorkflowIdsJson = app.WorkflowIdsJson;
        existing.AllowAnonymous = app.AllowAnonymous;
        existing.RequiredRole = app.RequiredRole;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync("update", "app", existing.Id, existing.Name);
        return existing;
    }

    public async Task<AppDefinition> PublishAsync(string id, bool publish)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var app = await db.AppDefinitions.FirstOrDefaultAsync(a => a.TenantId == tenant.TenantId && a.Id == id)
            ?? throw new KeyNotFoundException("App not found.");
        app.IsPublished = publish;
        app.PublishedAt = publish ? DateTime.UtcNow : app.PublishedAt;
        if (publish) app.Version++;
        app.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        if (publish)
            await versions.SnapshotAsync("app", app.Id, app.Name, app.Version, JsonUtil.Serialize(app), "publish");
        await audit.LogAsync(publish ? "publish" : "unpublish", "app", app.Id, app.Name);
        return app;
    }

    public async Task DeleteAsync(string id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var app = await db.AppDefinitions.FirstOrDefaultAsync(a => a.TenantId == tenant.TenantId && a.Id == id);
        if (app is null) return;
        db.AppDefinitions.Remove(app);
        await db.SaveChangesAsync();
        await audit.LogAsync("delete", "app", id, app.Name);
    }
}

// ---------------------------------------------------------------- Snippets

public interface ISnippetService
{
    Task<List<CodeSnippet>> GetAllAsync(string? category = null, string? search = null);
    Task<CodeSnippet> SaveAsync(CodeSnippet snippet);
    Task DeleteAsync(string id);
}

public class SnippetService(IDbContextFactory<ApplicationDbContext> dbFactory) : ISnippetService
{
    public async Task<List<CodeSnippet>> GetAllAsync(string? category = null, string? search = null)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = db.CodeSnippets.AsNoTracking();
        if (!string.IsNullOrEmpty(category)) q = q.Where(s => s.Category == category);
        if (!string.IsNullOrEmpty(search))
            q = q.Where(s => s.Title.Contains(search) || s.Tags.Contains(search) || s.Code.Contains(search));
        return await q.OrderBy(s => s.Category).ThenBy(s => s.Title).ToListAsync();
    }

    public async Task<CodeSnippet> SaveAsync(CodeSnippet snippet)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.CodeSnippets.FirstOrDefaultAsync(s => s.Id == snippet.Id);
        if (existing is null) db.CodeSnippets.Add(snippet);
        else db.Entry(existing).CurrentValues.SetValues(snippet);
        await db.SaveChangesAsync();
        return snippet;
    }

    public async Task DeleteAsync(string id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.CodeSnippets.Where(s => s.Id == id).ExecuteDeleteAsync();
    }
}

using AppBender.Core.Common;
using AppBender.Core.Models;

namespace AppBender.Core.Services;

public class ExportPackage
{
    public string Type { get; set; } = "appbender-export";
    public int FormatVersion { get; set; } = 1;
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    public List<EntityDefinition> Entities { get; set; } = [];
    public Dictionary<string, List<Dictionary<string, object?>>> Records { get; set; } = [];
    public List<FormDefinition> Forms { get; set; } = [];
    public List<WorkflowDefinition> Workflows { get; set; } = [];
    public List<AppDefinition> Apps { get; set; } = [];
}

public record ImportResult(int Entities, int Records, int Forms, int Workflows, int Apps, List<string> Warnings);

public interface IImportExportService
{
    /// <summary>Exports schema, forms, workflows and apps (optionally with entity records) as JSON.</summary>
    Task<string> ExportAsync(bool includeRecords = false);
    Task<ImportResult> ImportAsync(string json);
}

public class ImportExportService(
    IDataHubService dataHub,
    IFormService forms,
    IWorkflowService workflows,
    IAppService apps,
    IAuditService audit) : IImportExportService
{
    public async Task<string> ExportAsync(bool includeRecords = false)
    {
        var package = new ExportPackage
        {
            Entities = await dataHub.GetEntitiesAsync(),
            Forms = await forms.GetAllAsync(),
            Workflows = await workflows.GetAllAsync(),
            Apps = await apps.GetAllAsync()
        };
        if (includeRecords)
        {
            foreach (var entity in package.Entities)
            {
                var result = await dataHub.QueryAsync(entity.Name, new QueryOptions { PageSize = 500 });
                package.Records[entity.Name] = result.Records.Select(r =>
                {
                    var data = r.Data;
                    data["id"] = r.Id;
                    return data;
                }).ToList();
            }
        }
        await audit.LogAsync("export", "package", "all",
            $"entities={package.Entities.Count} forms={package.Forms.Count} workflows={package.Workflows.Count}");
        return JsonUtil.Serialize(package, indented: true);
    }

    public async Task<ImportResult> ImportAsync(string json)
    {
        var package = JsonUtil.Deserialize<ExportPackage>(json)
            ?? throw new InvalidOperationException("Invalid export file.");
        var warnings = new List<string>();
        int entityCount = 0, recordCount = 0, formCount = 0, workflowCount = 0, appCount = 0;

        foreach (var entity in package.Entities)
        {
            try
            {
                // adopt existing id when an entity of the same name exists, so it updates in place
                var existing = await dataHub.GetEntityAsync(entity.Name);
                if (existing is not null) entity.Id = existing.Id;
                await dataHub.SaveEntityAsync(entity, "import");
                entityCount++;
            }
            catch (Exception ex) { warnings.Add($"Entity '{entity.Name}': {ex.Message}"); }
        }

        foreach (var (entityName, rows) in package.Records)
        {
            foreach (var row in rows)
            {
                try
                {
                    row.Remove("id");
                    await dataHub.CreateRecordAsync(entityName, row);
                    recordCount++;
                }
                catch (Exception ex)
                {
                    warnings.Add($"Record in '{entityName}': {ex.Message}");
                    if (warnings.Count > 20) break;
                }
            }
        }

        foreach (var form in package.Forms)
        {
            try
            {
                var existing = await forms.GetBySlugAsync(form.Slug);
                if (existing is not null) form.Id = existing.Id;
                await forms.SaveAsync(form, "import");
                formCount++;
            }
            catch (Exception ex) { warnings.Add($"Form '{form.Name}': {ex.Message}"); }
        }

        foreach (var workflow in package.Workflows)
        {
            try
            {
                await workflows.SaveAsync(workflow, "import");
                workflowCount++;
            }
            catch (Exception ex) { warnings.Add($"Workflow '{workflow.Name}': {ex.Message}"); }
        }

        foreach (var appDef in package.Apps)
        {
            try
            {
                var existing = await apps.GetBySlugAsync(appDef.Slug);
                if (existing is not null) appDef.Id = existing.Id;
                else appDef.Slug = ""; // regenerate to avoid cross-tenant collisions
                await apps.SaveAsync(appDef);
                appCount++;
            }
            catch (Exception ex) { warnings.Add($"App '{appDef.Name}': {ex.Message}"); }
        }

        await audit.LogAsync("import", "package", "all",
            $"entities={entityCount} records={recordCount} forms={formCount} workflows={workflowCount} apps={appCount}");
        return new ImportResult(entityCount, recordCount, formCount, workflowCount, appCount, warnings);
    }
}

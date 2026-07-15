using AppBender.Core.Common;
using AppBender.Core.Models;
using AppBender.Core.Services;
using AppBender.Core.Workflows;

namespace AppBender.Core.AI;

public record PromptToAppResult(List<EntityDefinition> Entities, List<FormDefinition> Forms,
    List<WorkflowDefinition> Workflows, AppDefinition? App, string Summary);

public record DashboardWidget(string Title, string ChartType, string Entity, string? GroupBy,
    string? ValueField, string? Aggregate);

public interface IAiStudioService
{
    /// <summary>AI Schema Generator: description → entities created in the Data Hub.</summary>
    Task<List<EntityDefinition>> GenerateSchemaAsync(string description, bool create = true, CancellationToken ct = default);
    /// <summary>Prompt-to-App: description → schema + forms + workflows (+ app shell).</summary>
    Task<PromptToAppResult> GenerateAppAsync(string description, CancellationToken ct = default);
    /// <summary>AI Workflow Assistant: description → suggested workflow definition (not saved).</summary>
    Task<WorkflowDefinition> SuggestWorkflowAsync(string description, CancellationToken ct = default);
    /// <summary>Auto-Dashboard Generator: entity → chart widget suggestions.</summary>
    Task<List<DashboardWidget>> GenerateDashboardAsync(string entityName, CancellationToken ct = default);
    /// <summary>AI-Assisted Testing: generates test cases for a form or workflow definition.</summary>
    Task<string> GenerateTestCasesAsync(string itemType, string definitionJson, CancellationToken ct = default);
    /// <summary>AI Deployment Advisor: platform stats → scaling/DB/storage recommendations.</summary>
    Task<string> GetDeploymentAdviceAsync(CancellationToken ct = default);
}

public class AiStudioService(
    ILlmClient llm,
    IDataHubService dataHub,
    IFormService forms,
    IWorkflowService workflows,
    IAppService apps) : IAiStudioService
{
    private async Task<string> AskJsonAsync(string system, string prompt, CancellationToken ct)
    {
        var result = await llm.CompleteAsync(
            [new LlmMessage("system", system + " Reply with ONLY valid JSON, no markdown fences, no prose."),
             new LlmMessage("user", prompt)],
            new LlmRequestOptions { JsonMode = true, Temperature = 0.2 }, ct);
        return AiExtractAction.StripFences(result.Text);
    }

    // ---------------------------------------------------------------- schema

    private const string SchemaFormat = """
        {"entities":[{"name":"snake_case","displayName":"...","icon":"one emoji","description":"...",
        "fields":[{"name":"snake_case","displayName":"...","type":"Text|LongText|Number|Decimal|Currency|Boolean|Date|DateTime|Choice|MultiChoice|Email|Url|Phone|Image|AutoNumber","required":false,"options":["only for Choice"]}],
        "relationships":[{"name":"...","fromEntity":"parent","toEntity":"child","lookupField":"field_on_child"}]}]}
        """;

    public async Task<List<EntityDefinition>> GenerateSchemaAsync(string description, bool create = true, CancellationToken ct = default)
    {
        var json = await AskJsonAsync(
            "You design database schemas for a low-code platform. Output JSON in exactly this shape:\n" + SchemaFormat,
            $"Design entities for: {description}", ct);

        var parsed = JsonUtil.Deserialize<System.Text.Json.JsonElement>(json);
        var entities = new List<EntityDefinition>();
        if (parsed.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !parsed.TryGetProperty("entities", out var list))
            throw new InvalidOperationException("AI returned an unexpected schema format. Try again or refine the description.");

        foreach (var item in list.EnumerateArray())
        {
            var entity = new EntityDefinition
            {
                Name = item.GetProperty("name").GetString() ?? "entity",
                DisplayName = item.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "",
                Description = item.TryGetProperty("description", out var ds) ? ds.GetString() : null,
                Icon = item.TryGetProperty("icon", out var ic) ? ic.GetString() ?? "📦" : "📦",
                Fields = ParseFields(item),
                Relationships = ParseRelationships(item)
            };
            if (string.IsNullOrEmpty(entity.DisplayName)) entity.DisplayName = entity.Name;
            if (create) entity = await dataHub.SaveEntityAsync(entity, "AI generated");
            entities.Add(entity);
        }
        return entities;
    }

    private static List<FieldDefinition> ParseFields(System.Text.Json.JsonElement entity)
    {
        var fields = new List<FieldDefinition>();
        if (!entity.TryGetProperty("fields", out var list)) return fields;
        foreach (var f in list.EnumerateArray())
        {
            var field = new FieldDefinition
            {
                Name = f.GetProperty("name").GetString() ?? "field",
                DisplayName = f.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "",
                Required = f.TryGetProperty("required", out var rq) && rq.ValueKind == System.Text.Json.JsonValueKind.True
            };
            if (string.IsNullOrEmpty(field.DisplayName)) field.DisplayName = field.Name;
            if (f.TryGetProperty("type", out var t) && Enum.TryParse<FieldType>(t.GetString(), true, out var ft))
                field.Type = ft;
            if (f.TryGetProperty("options", out var opts) && opts.ValueKind == System.Text.Json.JsonValueKind.Array)
                field.Options = opts.EnumerateArray().Select(o => o.GetString() ?? "").Where(o => o.Length > 0).ToList();
            if (f.TryGetProperty("lookupEntity", out var le)) field.LookupEntity = le.GetString();
            fields.Add(field);
        }
        return fields;
    }

    private static List<RelationshipDefinition> ParseRelationships(System.Text.Json.JsonElement entity)
    {
        var relationships = new List<RelationshipDefinition>();
        if (!entity.TryGetProperty("relationships", out var list) ||
            list.ValueKind != System.Text.Json.JsonValueKind.Array) return relationships;
        foreach (var r in list.EnumerateArray())
        {
            relationships.Add(new RelationshipDefinition
            {
                Name = r.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                FromEntity = r.TryGetProperty("fromEntity", out var fe) ? fe.GetString() ?? "" : "",
                ToEntity = r.TryGetProperty("toEntity", out var te) ? te.GetString() ?? "" : "",
                LookupField = r.TryGetProperty("lookupField", out var lf) ? lf.GetString() ?? "" : ""
            });
        }
        return relationships;
    }

    // ---------------------------------------------------------------- prompt-to-app

    public async Task<PromptToAppResult> GenerateAppAsync(string description, CancellationToken ct = default)
    {
        // 1. schema
        var entities = await GenerateSchemaAsync(description, create: true, ct);

        // 2. one form per entity, generated from the fields (deterministic, reliable)
        var createdForms = new List<FormDefinition>();
        foreach (var entity in entities)
        {
            var layout = new List<FormComponent>
            {
                new() { Type = ComponentTypes.Heading, Label = entity.DisplayName, Width = 12 }
            };
            foreach (var field in entity.Fields.Where(f => f.Type != FieldType.AutoNumber && f.Type != FieldType.Formula))
                layout.Add(FieldToComponent(field));

            var form = await forms.SaveAsync(new FormDefinition
            {
                Name = $"{entity.DisplayName} Form",
                Slug = $"{entity.Name}_form",
                Description = $"Auto-generated form for {entity.DisplayName}",
                Icon = entity.Icon,
                EntityName = entity.Name,
                Layout = layout,
                IsPublished = true
            }, "AI generated");
            createdForms.Add(form);
        }

        // 3. ask AI for a welcome workflow suggestion on the primary entity
        var createdWorkflows = new List<WorkflowDefinition>();
        if (entities.Count > 0)
        {
            var main = entities[0];
            var workflow = new WorkflowDefinition
            {
                Name = $"On new {main.DisplayName}",
                Description = $"Runs when a {main.DisplayName} record is created (AI generated).",
                TriggerType = TriggerType.EntityCreated,
                TriggerConfig = new Dictionary<string, string> { ["entityName"] = main.Name },
                Steps =
                [
                    new WorkflowStep
                    {
                        Type = "log", Name = "Log new record",
                        Config = new() { ["message"] = $"New {main.DisplayName}: {{{{trigger.record.id}}}}" }
                    }
                ]
            };
            createdWorkflows.Add(await workflows.SaveAsync(workflow, "AI generated"));
        }

        // 4. app shell bundling the forms
        AppDefinition? app = null;
        if (createdForms.Count > 0)
        {
            app = await apps.SaveAsync(new AppDefinition
            {
                Name = Truncate(description, 40),
                Description = $"Generated from prompt: {description}",
                FormIds = createdForms.Select(f => f.Id).ToList(),
                WorkflowIds = createdWorkflows.Select(w => w.Id).ToList(),
                HomeFormId = createdForms[0].Id
            });
        }

        var summary = $"Created {entities.Count} entities, {createdForms.Count} forms, " +
                      $"{createdWorkflows.Count} workflows" + (app is not null ? $", and app '{app.Name}'." : ".");
        return new PromptToAppResult(entities, createdForms, createdWorkflows, app, summary);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    public static FormComponent FieldToComponent(FieldDefinition field)
    {
        var component = new FormComponent
        {
            Label = field.DisplayName,
            Field = field.Name,
            Required = field.Required,
            Width = field.Type is FieldType.LongText or FieldType.RichText ? 12 : 6
        };
        component.Type = field.Type switch
        {
            FieldType.LongText or FieldType.RichText => ComponentTypes.TextArea,
            FieldType.Number or FieldType.Decimal or FieldType.Currency => ComponentTypes.Number,
            FieldType.Boolean => ComponentTypes.Toggle,
            FieldType.Date => ComponentTypes.Date,
            FieldType.DateTime => ComponentTypes.DateTimeLocal,
            FieldType.Time => ComponentTypes.Time,
            FieldType.Choice => ComponentTypes.Dropdown,
            FieldType.MultiChoice => ComponentTypes.CheckboxList,
            FieldType.Email => ComponentTypes.Email,
            FieldType.Url => ComponentTypes.Url,
            FieldType.Phone => ComponentTypes.Phone,
            FieldType.Image => ComponentTypes.ImageUpload,
            FieldType.File => ComponentTypes.FileUpload,
            FieldType.Lookup => ComponentTypes.Lookup,
            _ => ComponentTypes.TextBox
        };
        if (field.Options.Count > 0)
            component.Props["options"] = string.Join(",", field.Options);
        if (field.Type == FieldType.Lookup && !string.IsNullOrEmpty(field.LookupEntity))
            component.Props["entity"] = field.LookupEntity;
        return component;
    }

    // ---------------------------------------------------------------- workflow assistant

    private static readonly string WorkflowFormat = """
        {"name":"...","description":"...","triggerType":"Manual|Schedule|Webhook|EntityCreated|EntityUpdated|EntityDeleted|FormSubmitted",
        "triggerConfig":{"cron":"*/5 * * * *","entityName":"...","webhookKey":"..."},
        "steps":[{"type":"ACTION_TYPE","name":"...","config":{"key":"value"},
                  "trueSteps":[],"falseSteps":[],"children":[]}]}
        """;

    public async Task<WorkflowDefinition> SuggestWorkflowAsync(string description, CancellationToken ct = default)
    {
        var actionList = string.Join("\n", ActionCatalog.All.Select(a =>
            $"- {a.Type}: {a.Description} config keys: {string.Join(",", a.ConfigFields.Select(c => c.Key))}"));
        var json = await AskJsonAsync(
            "You design workflow automations for a low-code platform (like Power Automate). " +
            "Available step types:\n" + actionList +
            "\nTemplates {{trigger.x}}, {{vars.x}}, {{steps.stepname.output}} may be used in config values. " +
            "Output JSON exactly in this shape:\n" + WorkflowFormat,
            $"Design a workflow for: {description}", ct);

        var suggestion = JsonUtil.Deserialize<System.Text.Json.JsonElement>(json);
        var workflow = new WorkflowDefinition
        {
            Name = suggestion.TryGetProperty("name", out var n) ? n.GetString() ?? "AI Workflow" : "AI Workflow",
            Description = suggestion.TryGetProperty("description", out var d) ? d.GetString() : description
        };
        if (suggestion.TryGetProperty("triggerType", out var tt) &&
            Enum.TryParse<TriggerType>(tt.GetString(), true, out var trigger))
            workflow.TriggerType = trigger;
        if (suggestion.TryGetProperty("triggerConfig", out var tc) && tc.ValueKind == System.Text.Json.JsonValueKind.Object)
            workflow.TriggerConfig = tc.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.ValueKind == System.Text.Json.JsonValueKind.String
                    ? p.Value.GetString() ?? "" : p.Value.GetRawText());
        if (suggestion.TryGetProperty("steps", out var steps))
            workflow.Steps = JsonUtil.Deserialize<List<WorkflowStep>>(steps.GetRawText()) ?? [];
        return workflow;
    }

    // ---------------------------------------------------------------- dashboard generator

    public async Task<List<DashboardWidget>> GenerateDashboardAsync(string entityName, CancellationToken ct = default)
    {
        var entity = await dataHub.GetEntityAsync(entityName)
            ?? throw new KeyNotFoundException($"Entity '{entityName}' not found.");
        var fieldList = string.Join(", ", entity.Fields.Select(f => $"{f.Name} ({f.Type})"));

        var json = await AskJsonAsync(
            "You design analytics dashboards. Choose 3-5 chart widgets for the given dataset. " +
            "chartType: bar|line|pie|stat. groupBy must be a Choice/Boolean/Date/Text field; " +
            "valueField numeric or null (null = count). aggregate: count|sum|avg. Output:\n" +
            """{"widgets":[{"title":"...","chartType":"bar","groupBy":"field","valueField":null,"aggregate":"count"}]}""",
            $"Dataset '{entity.DisplayName}' fields: {fieldList}", ct);

        var parsed = JsonUtil.Deserialize<System.Text.Json.JsonElement>(json);
        var widgets = new List<DashboardWidget>();
        if (parsed.ValueKind == System.Text.Json.JsonValueKind.Object &&
            parsed.TryGetProperty("widgets", out var list))
        {
            foreach (var w in list.EnumerateArray())
            {
                widgets.Add(new DashboardWidget(
                    w.TryGetProperty("title", out var t) ? t.GetString() ?? "Chart" : "Chart",
                    w.TryGetProperty("chartType", out var c) ? c.GetString() ?? "bar" : "bar",
                    entityName,
                    w.TryGetProperty("groupBy", out var g) ? g.GetString() : null,
                    w.TryGetProperty("valueField", out var v) ? v.GetString() : null,
                    w.TryGetProperty("aggregate", out var a) ? a.GetString() : "count"));
            }
        }
        return widgets;
    }

    // ---------------------------------------------------------------- testing & deployment

    public async Task<string> GenerateTestCasesAsync(string itemType, string definitionJson, CancellationToken ct = default)
    {
        var result = await llm.CompleteAsync(
            [new LlmMessage("system",
                "You are a QA engineer. Generate concise, actionable test cases in markdown: " +
                "a table with columns No, Scenario, Steps, Input, Expected Result. Include happy paths, " +
                "validation failures and edge cases."),
             new LlmMessage("user", $"Generate test cases for this {itemType} definition:\n```json\n{definitionJson}\n```")],
            new LlmRequestOptions { Temperature = 0.3 }, ct);
        return result.Text;
    }

    public async Task<string> GetDeploymentAdviceAsync(CancellationToken ct = default)
    {
        var entities = await dataHub.GetEntitiesAsync();
        var stats = new System.Text.StringBuilder();
        stats.AppendLine($"Entities: {entities.Count}");
        foreach (var entity in entities.Take(20))
            stats.AppendLine($"- {entity.Name}: {await dataHub.CountRecordsAsync(entity.Name)} records");
        var allForms = await forms.GetAllAsync();
        var allWorkflows = await workflows.GetAllAsync();
        stats.AppendLine($"Forms: {allForms.Count}, Workflows: {allWorkflows.Count} " +
                         $"({allWorkflows.Count(w => w.TriggerType == TriggerType.Schedule)} scheduled)");

        var result = await llm.CompleteAsync(
            [new LlmMessage("system",
                "You are a deployment advisor for AppBender (a .NET 10 Blazor Server low-code platform, " +
                "currently on SQLite + local file storage). Give concrete recommendations in markdown: " +
                "database choice (SQLite vs PostgreSQL vs SQL Server), storage (FileSystem vs S3/MinIO vs Azure Blob), " +
                "scaling (single node vs load-balanced with sticky sessions for Blazor Server), backups, and monitoring. " +
                "Be specific to the given usage stats."),
             new LlmMessage("user", $"Current platform usage:\n{stats}")],
            new LlmRequestOptions { Temperature = 0.4 }, ct);
        return result.Text;
    }
}

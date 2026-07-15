using AppBender.Core.Data;
using AppBender.Core.Models;
using AppBender.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AppBender.Core.Connectors;

public class ConnectorRuntime(
    IEnumerable<IConnector> providers,
    IDbContextFactory<ApplicationDbContext> dbFactory,
    ITenantContext tenant,
    IAuditService audit,
    IUsageService usage) : IConnectorRuntime
{
    public IReadOnlyList<IConnector> Providers { get; } = providers.ToList();

    public async Task<List<ConnectorDefinition>> GetDefinitionsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ConnectorDefinitions.AsNoTracking()
            .Where(c => c.TenantId == tenant.TenantId)
            .OrderBy(c => c.Category).ThenBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<ConnectorDefinition?> GetDefinitionAsync(string idOrName)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ConnectorDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenant.TenantId && (c.Id == idOrName || c.Name == idOrName));
    }

    public async Task<ConnectorDefinition> SaveDefinitionAsync(ConnectorDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
            throw new InvalidOperationException("Connector name is required.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.ConnectorDefinitions
            .FirstOrDefaultAsync(c => c.TenantId == tenant.TenantId && c.Id == definition.Id);
        if (existing is null)
        {
            definition.TenantId = tenant.TenantId;
            db.ConnectorDefinitions.Add(definition);
            await db.SaveChangesAsync();
            await audit.LogAsync("create", "connector", definition.Id, definition.Name);
            return definition;
        }
        existing.Name = definition.Name;
        existing.Provider = definition.Provider;
        existing.Category = definition.Category;
        existing.Description = definition.Description;
        existing.Icon = definition.Icon;
        existing.IsCustom = definition.IsCustom;
        existing.SpecJson = definition.SpecJson;
        existing.ConfigJson = definition.ConfigJson;
        existing.IsEnabled = definition.IsEnabled;
        existing.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        await audit.LogAsync("update", "connector", existing.Id, existing.Name);
        return existing;
    }

    public async Task DeleteDefinitionAsync(string id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var definition = await db.ConnectorDefinitions
            .FirstOrDefaultAsync(c => c.TenantId == tenant.TenantId && c.Id == id);
        if (definition is null) return;
        db.ConnectorDefinitions.Remove(definition);
        await db.SaveChangesAsync();
        await audit.LogAsync("delete", "connector", id, definition.Name);
    }

    public async Task<object?> ExecuteAsync(string connectorIdOrName, string action,
        Dictionary<string, object?> input, CancellationToken ct = default)
    {
        var definition = await GetDefinitionAsync(connectorIdOrName)
            ?? throw new InvalidOperationException($"Connector '{connectorIdOrName}' not found.");
        if (!definition.IsEnabled)
            throw new InvalidOperationException($"Connector '{definition.Name}' is disabled.");
        var provider = Providers.FirstOrDefault(p =>
            p.Provider.Equals(definition.Provider, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"No provider '{definition.Provider}' is registered.");
        usage.TrackFireAndForget("connector_call", 1, $"{definition.Name}:{action}");
        return await provider.ExecuteAsync(definition, action, input, ct);
    }
}

/// <summary>Cross-connector sync: pull external rows into a Data Hub entity, or push entity records out.</summary>
public interface IDataSyncService
{
    /// <summary>Runs the connector action, expects a list of objects, and upserts them into the entity by key field.</summary>
    Task<(int Created, int Updated)> PullAsync(string connectorIdOrName, string action,
        Dictionary<string, object?> input, string entityName, string keyField, CancellationToken ct = default);
    /// <summary>Runs the connector action once per entity record, passing the record as the action input.</summary>
    Task<int> PushAsync(string entityName, string connectorIdOrName, string action, CancellationToken ct = default);
}

public class DataSyncService(IConnectorRuntime connectors, IDataHubService dataHub, IAuditService audit) : IDataSyncService
{
    public async Task<(int Created, int Updated)> PullAsync(string connectorIdOrName, string action,
        Dictionary<string, object?> input, string entityName, string keyField, CancellationToken ct = default)
    {
        var result = await connectors.ExecuteAsync(connectorIdOrName, action, input, ct);
        var rows = ExtractRows(result);
        int created = 0, updated = 0;

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            if (!row.TryGetValue(keyField, out var keyValue) || keyValue is null) continue;
            var existing = await dataHub.QueryAsync(entityName, new QueryOptions
            {
                Filters = [new FieldFilter { Field = keyField, Op = "eq", Value = Common.TemplateEngine.ToText(keyValue) }],
                PageSize = 1
            });
            if (existing.Records.Count > 0)
            {
                await dataHub.UpdateRecordAsync(entityName, existing.Records[0].Id, row);
                updated++;
            }
            else
            {
                await dataHub.CreateRecordAsync(entityName, row);
                created++;
            }
        }

        await audit.LogAsync("sync_pull", "entity", entityName,
            $"connector={connectorIdOrName} created={created} updated={updated}");
        return (created, updated);
    }

    public async Task<int> PushAsync(string entityName, string connectorIdOrName, string action, CancellationToken ct = default)
    {
        var records = await dataHub.QueryAsync(entityName, new QueryOptions { PageSize = 500 });
        var pushed = 0;
        foreach (var record in records.Records)
        {
            ct.ThrowIfCancellationRequested();
            var payload = record.Data;
            payload["id"] = record.Id;
            await connectors.ExecuteAsync(connectorIdOrName, action, payload, ct);
            pushed++;
        }
        await audit.LogAsync("sync_push", "entity", entityName, $"connector={connectorIdOrName} pushed={pushed}");
        return pushed;
    }

    private static List<Dictionary<string, object?>> ExtractRows(object? result)
    {
        // accept a bare list, or an HTTP-style envelope { body: [...] } / { body: { data: [...] } }
        if (result is Dictionary<string, object?> envelope)
        {
            if (envelope.TryGetValue("body", out var body)) return ExtractRows(body);
            if (envelope.TryGetValue("data", out var data)) return ExtractRows(data);
            return [envelope];
        }
        if (result is List<object?> list)
            return list.OfType<Dictionary<string, object?>>().ToList();
        return [];
    }
}

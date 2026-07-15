using AppBender.Core.Models;

namespace AppBender.Core.Connectors;

/// <summary>A built-in connector provider (rest, sqlserver, s3, smtp, ...).</summary>
public interface IConnector
{
    /// <summary>Provider key stored in ConnectorDefinition.Provider.</summary>
    string Provider { get; }
    string DisplayName { get; }
    string Category { get; }
    string Icon { get; }
    /// <summary>Config fields required when creating an instance (shown in the UI).</summary>
    IReadOnlyList<string> ConfigKeys { get; }
    IReadOnlyList<ConnectorActionDescriptor> Actions { get; }
    /// <summary>Executes an action using instance config + rendered input parameters.</summary>
    Task<object?> ExecuteAsync(ConnectorDefinition definition, string action,
        Dictionary<string, object?> input, CancellationToken ct = default);
}

/// <summary>Resolves connector definitions and dispatches actions to providers.</summary>
public interface IConnectorRuntime
{
    IReadOnlyList<IConnector> Providers { get; }
    Task<List<ConnectorDefinition>> GetDefinitionsAsync();
    Task<ConnectorDefinition?> GetDefinitionAsync(string idOrName);
    Task<ConnectorDefinition> SaveDefinitionAsync(ConnectorDefinition definition);
    Task DeleteDefinitionAsync(string id);
    Task<object?> ExecuteAsync(string connectorIdOrName, string action,
        Dictionary<string, object?> input, CancellationToken ct = default);
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AppBender.Core.Common;

namespace AppBender.Core.Models;

/// <summary>Describes one action a connector can perform (for designer UIs).</summary>
public class ConnectorActionDescriptor
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Input parameter names shown in the designer.</summary>
    public List<string> Inputs { get; set; } = [];
}

/// <summary>One HTTP operation of a custom JSON-defined connector.</summary>
public class CustomConnectorAction
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Method { get; set; } = "GET";
    /// <summary>Path appended to baseUrl; supports {{param}} templates.</summary>
    public string PathTemplate { get; set; } = "/";
    public Dictionary<string, string> Headers { get; set; } = [];
    /// <summary>Optional request body template ({{param}} supported).</summary>
    public string? BodyTemplate { get; set; }
    public List<string> Inputs { get; set; } = [];
}

/// <summary>JSON spec of a custom connector (Custom Connector Builder output).</summary>
public class CustomConnectorSpec
{
    public string BaseUrl { get; set; } = "";
    /// <summary>none | apikey_header | apikey_query | bearer | basic</summary>
    public string AuthType { get; set; } = "none";
    /// <summary>Header/query parameter name used for apikey auth.</summary>
    public string? AuthParamName { get; set; }
    public Dictionary<string, string> DefaultHeaders { get; set; } = [];
    public List<CustomConnectorAction> Actions { get; set; } = [];
}

/// <summary>A configured connector instance (built-in or custom) stored in the library.</summary>
public class ConnectorDefinition
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Provider key: rest, graphql, sqlserver, postgres, mysql, sqlite, s3, azureblob, filesystem, smtp, webhook, tavily, custom.</summary>
    [MaxLength(40)] public string Provider { get; set; } = "rest";
    public string Category { get; set; } = "General";
    public string? Description { get; set; }
    public string Icon { get; set; } = "🔌";
    public bool IsCustom { get; set; }
    /// <summary>For custom connectors: the CustomConnectorSpec JSON.</summary>
    public string SpecJson { get; set; } = "{}";
    /// <summary>Instance configuration: connection string, api key, bucket, from-address...</summary>
    public string ConfigJson { get; set; } = "{}";
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public CustomConnectorSpec Spec
    {
        get => JsonUtil.DeserializeOrNew<CustomConnectorSpec>(SpecJson);
        set => SpecJson = JsonUtil.Serialize(value);
    }

    [NotMapped]
    public Dictionary<string, string> Config
    {
        get => JsonUtil.DeserializeOrNew<Dictionary<string, string>>(ConfigJson);
        set => ConfigJson = JsonUtil.Serialize(value);
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AppBender.Core.Common;

namespace AppBender.Core.Models;

public enum FieldType
{
    Text, LongText, RichText, Number, Decimal, Currency, Boolean,
    Date, DateTime, Time, Choice, MultiChoice, Lookup,
    Email, Url, Phone, File, Image, Json, Formula, AutoNumber
}

public class FieldDefinition
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public FieldType Type { get; set; } = FieldType.Text;
    public bool Required { get; set; }
    public bool Unique { get; set; }
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
    /// <summary>Choice/MultiChoice options.</summary>
    public List<string> Options { get; set; } = [];
    /// <summary>Target entity name for Lookup fields.</summary>
    public string? LookupEntity { get; set; }
    /// <summary>Expression for Formula fields, e.g. "price * qty".</summary>
    public string? Formula { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public int? MaxLength { get; set; }
}

public enum RelationshipType { OneToMany, ManyToMany }

public class RelationshipDefinition
{
    public string Name { get; set; } = "";
    public RelationshipType Type { get; set; } = RelationshipType.OneToMany;
    /// <summary>The "one" side (parent) entity name.</summary>
    public string FromEntity { get; set; } = "";
    /// <summary>The "many" side (child) entity name.</summary>
    public string ToEntity { get; set; } = "";
    /// <summary>Lookup field on the child entity holding the parent id.</summary>
    public string LookupField { get; set; } = "";
}

/// <summary>A Data Hub entity (table) definition. Fields/relationships are stored as JSON.</summary>
public class EntityDefinition
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = "";
    /// <summary>Logical name, unique per tenant (e.g. "customers").</summary>
    [MaxLength(80)] public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public string Icon { get; set; } = "📦";
    public string FieldsJson { get; set; } = "[]";
    public string RelationshipsJson { get; set; } = "[]";
    public int Version { get; set; } = 1;
    public bool IsSystem { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public List<FieldDefinition> Fields
    {
        get => JsonUtil.DeserializeOrNew<List<FieldDefinition>>(FieldsJson);
        set => FieldsJson = JsonUtil.Serialize(value);
    }

    [NotMapped]
    public List<RelationshipDefinition> Relationships
    {
        get => JsonUtil.DeserializeOrNew<List<RelationshipDefinition>>(RelationshipsJson);
        set => RelationshipsJson = JsonUtil.Serialize(value);
    }
}

/// <summary>A row of a dynamic entity; values live in DataJson.</summary>
public class DataRecord
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = "";
    [MaxLength(80)] public string EntityName { get; set; } = "";
    public string DataJson { get; set; } = "{}";
    public int Version { get; set; } = 1;
    public string? CreatedById { get; set; }
    public string? UpdatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public Dictionary<string, object?> Data
    {
        get => JsonUtil.ToClrDictionary(DataJson);
        set => DataJson = JsonUtil.Serialize(value);
    }
}

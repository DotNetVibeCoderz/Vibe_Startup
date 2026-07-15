using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AppBender.Core.Common;

namespace AppBender.Core.Models;

/// <summary>A published low-code application: a bundle of forms + workflows with a public URL.</summary>
public class AppDefinition
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
    [MaxLength(80)] public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string Icon { get; set; } = "🚀";
    public string Color { get; set; } = "#4f6ef7";
    public string? HomeFormId { get; set; }
    public string FormIdsJson { get; set; } = "[]";
    public string WorkflowIdsJson { get; set; } = "[]";
    public bool IsPublished { get; set; }
    public bool AllowAnonymous { get; set; }
    /// <summary>Minimum role required when not anonymous: EndUser, Developer, Admin.</summary>
    public string RequiredRole { get; set; } = "EndUser";
    public int Version { get; set; } = 1;
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public List<string> FormIds
    {
        get => JsonUtil.DeserializeOrNew<List<string>>(FormIdsJson);
        set => FormIdsJson = JsonUtil.Serialize(value);
    }

    [NotMapped]
    public List<string> WorkflowIds
    {
        get => JsonUtil.DeserializeOrNew<List<string>>(WorkflowIdsJson);
        set => WorkflowIdsJson = JsonUtil.Serialize(value);
    }
}

public class Organization
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    [MaxLength(80)] public string Slug { get; set; } = "";
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

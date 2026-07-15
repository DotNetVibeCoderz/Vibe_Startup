using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AppBender.Core.Common;

namespace AppBender.Core.Models;

public class ChatSession
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string Title { get; set; } = "New chat";
    /// <summary>Optional per-session overrides; defaults come from appsettings AI section.</summary>
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? SystemPromptOverride { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ChatAttachment
{
    public string FileName { get; set; } = "";
    public string Url { get; set; } = "";
    public string ContentType { get; set; } = "";
    public bool IsImage { get; set; }
}

public class ChatMessage
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SessionId { get; set; } = "";
    /// <summary>user | assistant | system | tool</summary>
    public string Role { get; set; } = "user";
    public string Content { get; set; } = "";
    public string AttachmentsJson { get; set; } = "[]";
    public int TokensIn { get; set; }
    public int TokensOut { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public List<ChatAttachment> Attachments
    {
        get => JsonUtil.DeserializeOrNew<List<ChatAttachment>>(AttachmentsJson);
        set => AttachmentsJson = JsonUtil.Serialize(value);
    }
}

/// <summary>Uploaded document indexed into the RAG knowledge base.</summary>
public class KnowledgeDocument
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public string StoragePath { get; set; } = "";
    public long SizeBytes { get; set; }
    /// <summary>pending | indexed | failed</summary>
    public string Status { get; set; } = "pending";
    public string? Error { get; set; }
    public int ChunkCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class KnowledgeChunk
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = "";
    public string DocumentId { get; set; } = "";
    public string FileName { get; set; } = "";
    public int ChunkIndex { get; set; }
    public string Text { get; set; } = "";
    /// <summary>JSON float[] embedding; empty when only keyword search is available.</summary>
    public string EmbeddingJson { get; set; } = "";

    [NotMapped]
    public float[] Embedding
    {
        get => JsonUtil.Deserialize<float[]>(EmbeddingJson) ?? [];
        set => EmbeddingJson = JsonUtil.Serialize(value);
    }
}

/// <summary>A trained ML model built by the Model Builder over a Data Hub entity.</summary>
public class MlModelDefinition
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
    public string EntityName { get; set; } = "";
    public string TargetField { get; set; } = "";
    public string FeatureFieldsJson { get; set; } = "[]";
    /// <summary>linear_regression | knn_classifier</summary>
    public string ModelType { get; set; } = "linear_regression";
    /// <summary>Trained parameters (weights / training set).</summary>
    public string ParametersJson { get; set; } = "{}";
    public string MetricsJson { get; set; } = "{}";
    public DateTime? TrainedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public List<string> FeatureFields
    {
        get => JsonUtil.DeserializeOrNew<List<string>>(FeatureFieldsJson);
        set => FeatureFieldsJson = JsonUtil.Serialize(value);
    }
}

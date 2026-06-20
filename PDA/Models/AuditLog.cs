using System.ComponentModel.DataAnnotations;

namespace PDA.Models;

/// <summary>
/// Audit log for all system activities
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Action category: Auth, Chat, Database, Query, Config, RAG, Export, etc.
    /// </summary>
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Specific action: Login, Logout, QueryExecuted, DashboardGenerated, etc.
    /// </summary>
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    /// <summary>
    /// JSON detail data
    /// </summary>
    public string? Details { get; set; }

    [MaxLength(100)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public double? DurationMs { get; set; }

    public bool IsSuccess { get; set; } = true;

    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    // Foreign key
    [MaxLength(450)]
    public string? UserId { get; set; }

    // Navigation
    public ApplicationUser? User { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace Bioskop.Models;

/// <summary>
/// Audit log untuk mencatat aktivitas user
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    [MaxLength(50)]
    public string? UserId { get; set; }

    [MaxLength(100)]
    public string? UserName { get; set; }

    [Required, MaxLength(50)]
    public string Action { get; set; } = string.Empty; // Create, Read, Update, Delete, Login, Logout

    [Required, MaxLength(200)]
    public string EntityName { get; set; } = string.Empty; // Movie, Order, User, dll

    public string? EntityId { get; set; }

    public string? OldValues { get; set; } // JSON
    public string? NewValues { get; set; } // JSON

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Traffic access log untuk monitoring
/// </summary>
public class TrafficLog
{
    public long Id { get; set; }

    [MaxLength(500)]
    public string? Url { get; set; }

    [MaxLength(10)]
    public string? Method { get; set; } // GET, POST, dll

    public int StatusCode { get; set; }

    [MaxLength(50)]
    public string? UserId { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public long ResponseTimeMs { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

using System.ComponentModel.DataAnnotations;

namespace Comblang.Models;

/// <summary>
/// Web traffic analytics log recording page visits and user activity.
/// </summary>
public class TrafficLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(500)]
    public string? PageUrl { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [MaxLength(100)]
    public string? SessionId { get; set; }

    public Guid? UserId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

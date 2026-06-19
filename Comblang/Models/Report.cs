using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Comblang.Models;

/// <summary>
/// User reports for inappropriate behavior or policy violations.
/// </summary>
public class Report
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ReporterId { get; set; }

    [Required]
    public Guid ReportedUserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Reason { get; set; } = string.Empty; // "Fake", "Inappropriate", "Spam", "Harassment", "Other"

    [MaxLength(2000)]
    public string? Details { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // "Pending", "Reviewed", "Resolved", "Dismissed"

    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(ReporterId))]
    public User? Reporter { get; set; }

    [ForeignKey(nameof(ReportedUserId))]
    public User? ReportedUser { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace Comblang.Models;

/// <summary>
/// Community events (online & offline) for users.
/// </summary>
public class Event
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string EventType { get; set; } = "Online"; // "Online", "Offline"

    [MaxLength(500)]
    public string? Location { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public int MaxParticipants { get; set; } = 100;

    public Guid CreatedById { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();
}

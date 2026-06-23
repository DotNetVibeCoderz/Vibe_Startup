using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventSphere.Data.Models;

/// <summary>
/// Relasi many-to-many antara Event dan Guest (ApplicationUser)
/// </summary>
public class EventAttendee
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid EventId { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public AttendeeRole Role { get; set; } = AttendeeRole.Guest;
    public RsvpStatus RsvpStatus { get; set; } = RsvpStatus.Pending;
    
    public bool HasPlusOne { get; set; }
    public string? PlusOneName { get; set; }
    
    [MaxLength(500)]
    public string? DietaryRestrictions { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public DateTime? RsvpDate { get; set; }
    public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
    
    // Seating
    public Guid? TableId { get; set; }
    public int? SeatNumber { get; set; }
    
    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }
    
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
    
    [ForeignKey(nameof(TableId))]
    public TableArrangement? Table { get; set; }
}

public enum AttendeeRole
{
    Guest,
    Vip,
    Family,
    BridalParty,
    Speaker,
    Performer,
    Staff
}

public enum RsvpStatus
{
    Pending,
    Accepted,
    Declined,
    Maybe
}

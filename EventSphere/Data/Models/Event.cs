using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventSphere.Data.Models;

/// <summary>
/// Model utama untuk event/wedding
/// </summary>
public class Event
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [Required]
    public DateTime EventDate { get; set; }
    
    public DateTime? EndDate { get; set; }
    
    [MaxLength(500)]
    public string? Location { get; set; }
    
    [MaxLength(100)]
    public string? Theme { get; set; }
    
    [MaxLength(50)]
    public string? PrimaryColor { get; set; } = "#6C5CE7";
    
    [MaxLength(50)]
    public string? SecondaryColor { get; set; } = "#A29BFE";
    
    public EventStatus Status { get; set; } = EventStatus.Draft;
    
    public decimal BudgetTotal { get; set; }
    public decimal BudgetSpent { get; set; }
    
    public int ExpectedGuests { get; set; }
    public int ConfirmedGuests { get; set; }
    
    [MaxLength(50)]
    public string? EventType { get; set; } // Wedding, Birthday, Corporate, etc.
    
    // Foreign Keys
    [Required]
    public string CreatedById { get; set; } = string.Empty;
    public string? OrganizerId { get; set; }
    public string? ClientId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation Properties
    [ForeignKey(nameof(CreatedById))]
    public ApplicationUser? CreatedBy { get; set; }
    
    [ForeignKey(nameof(OrganizerId))]
    public ApplicationUser? Organizer { get; set; }
    
    [ForeignKey(nameof(ClientId))]
    public ApplicationUser? Client { get; set; }
    
    public ICollection<EventAttendee> Attendees { get; set; } = new List<EventAttendee>();
    public ICollection<VendorContract> VendorContracts { get; set; } = new List<VendorContract>();
    public ICollection<BudgetItem> BudgetItems { get; set; } = new List<BudgetItem>();
    public ICollection<TaskItem> TaskItems { get; set; } = new List<TaskItem>();
    public ICollection<MediaItem> MediaItems { get; set; } = new List<MediaItem>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    public ICollection<TableArrangement> TableArrangements { get; set; } = new List<TableArrangement>();
}

public enum EventStatus
{
    Draft,
    Planned,
    Confirmed,
    InProgress,
    Completed,
    Cancelled
}

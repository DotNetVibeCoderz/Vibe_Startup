using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventSphere.Data.Models;

/// <summary>
/// Item anggaran per event
/// </summary>
public class BudgetItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid EventId { get; set; }
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? Category { get; set; } // Venue, Catering, Dekorasi, etc.
    
    public decimal EstimatedCost { get; set; }
    public decimal ActualCost { get; set; }
    
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public int SortOrder { get; set; }
    
    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }
}

/// <summary>
/// Task & Checklist per event
/// </summary>
public class TaskItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid EventId { get; set; }
    
    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;
    
    [MaxLength(2000)]
    public string? Description { get; set; }
    
    [MaxLength(50)]
    public string? Category { get; set; }
    
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;
    
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    public string? AssignedToId { get; set; }
    public string? CompletedById { get; set; }
    
    public int SortOrder { get; set; }
    public int Progress { get; set; } // 0-100
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }
    
    [ForeignKey(nameof(AssignedToId))]
    public ApplicationUser? AssignedTo { get; set; }
    
    [ForeignKey(nameof(CompletedById))]
    public ApplicationUser? CompletedBy { get; set; }
}

public enum TaskPriority
{
    Low,
    Medium,
    High,
    Urgent
}

public enum TaskItemStatus
{
    Todo,
    InProgress,
    Review,
    Done,
    Cancelled
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WashUp.Models;

/// <summary>
/// Staff/employee record with schedule and performance data
/// </summary>
public class StaffMember
{
    public int Id { get; set; }
    
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    
    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string Position { get; set; } = string.Empty; // Admin, Operator, Courier, Manager
    
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(100)]
    public string? Email { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseSalary { get; set; }
    
    [MaxLength(50)]
    public string EmploymentType { get; set; } = "FullTime"; // FullTime, PartTime, Contract
    
    public DateTime HireDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public bool IsActive { get; set; } = true;
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public ICollection<StaffSchedule> Schedules { get; set; } = new List<StaffSchedule>();
    public ICollection<StaffPerformance> Performances { get; set; } = new List<StaffPerformance>();
    public ICollection<CourierAssignment> CourierAssignments { get; set; } = new List<CourierAssignment>();
}

/// <summary>
/// Staff work schedule
/// </summary>
public class StaffSchedule
{
    public int Id { get; set; }
    public int StaffMemberId { get; set; }
    public StaffMember? StaffMember { get; set; }
    
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Staff performance evaluation
/// </summary>
public class StaffPerformance
{
    public int Id { get; set; }
    public int StaffMemberId { get; set; }
    public StaffMember? StaffMember { get; set; }
    
    public int Month { get; set; }
    public int Year { get; set; }
    
    public int OrdersCompleted { get; set; }
    public double AverageRating { get; set; } // 1-5
    public int CustomerComplaints { get; set; }
    public int AttendanceDays { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal? Bonus { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartDrive.Models.Enums;

namespace SmartDrive.Models.Entities;

/// <summary>
/// Booking jadwal latihan
/// </summary>
public class Booking
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string StudentUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(StudentUserId))]
    public ApplicationUser? Student { get; set; }

    public int? InstructorId { get; set; }

    [ForeignKey(nameof(InstructorId))]
    public InstructorProfile? Instructor { get; set; }

    public int? VehicleId { get; set; }

    [ForeignKey(nameof(VehicleId))]
    public Vehicle? Vehicle { get; set; }

    public int? LocationId { get; set; }

    [ForeignKey(nameof(LocationId))]
    public TrainingLocation? Location { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Scheduled;

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? InstructorNotes { get; set; }

    public bool IsPaid { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CancelledAt { get; set; }

    [MaxLength(500)]
    public string? CancelReason { get; set; }

    // GPS tracking
    public double? StartLatitude { get; set; }
    public double? StartLongitude { get; set; }
    public double? EndLatitude { get; set; }
    public double? EndLongitude { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

/// <summary>
/// Jadwal ketersediaan instruktur
/// </summary>
public class InstructorSchedule
{
    [Key]
    public int Id { get; set; }

    public int InstructorId { get; set; }

    [ForeignKey(nameof(InstructorId))]
    public InstructorProfile? Instructor { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public bool IsAvailable { get; set; } = true;

    [MaxLength(200)]
    public string? Notes { get; set; }
}

/// <summary>
/// Lokasi latihan
/// </summary>
public class TrainingLocation
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Address { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    [MaxLength(50)]
    public string? LocationType { get; set; } // ParkingLot, Street, Highway, etc.

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

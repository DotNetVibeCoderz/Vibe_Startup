using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartDrive.Models.Entities;

/// <summary>
/// Profil instruktur mengemudi
/// </summary>
public class InstructorProfile
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [MaxLength(50)]
    public string? LicenseNumber { get; set; } // Nomor sertifikasi mengajar

    public int YearsOfExperience { get; set; }

    [MaxLength(1000)]
    public string? Bio { get; set; }

    [MaxLength(500)]
    public string? Certifications { get; set; } // JSON array sertifikasi

    [MaxLength(255)]
    public string? CertificationFilePath { get; set; }

    [Column(TypeName = "decimal(3,2)")]
    public decimal AverageRating { get; set; } = 5.0m;

    public int TotalRatings { get; set; }

    public int TotalStudents { get; set; }

    public int TotalHoursTaught { get; set; }

    public bool IsAvailable { get; set; } = true;

    [MaxLength(50)]
    public string? VehiclePlateNumber { get; set; } // Kendaraan yang biasa dipakai

    public ICollection<InstructorSchedule> Schedules { get; set; } = new List<InstructorSchedule>();
    public ICollection<Booking> AssignedBookings { get; set; } = new List<Booking>();
    public ICollection<StudentFeedback> Feedbacks { get; set; } = new List<StudentFeedback>();
}

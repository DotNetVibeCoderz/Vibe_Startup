using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartDrive.Models.Entities;

/// <summary>
/// Profil siswa yang sedang belajar menyetir
/// </summary>
public class StudentProfile
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    public int? AssignedInstructorId { get; set; }

    [ForeignKey(nameof(AssignedInstructorId))]
    public InstructorProfile? AssignedInstructor { get; set; }

    public int TotalHoursCompleted { get; set; }

    public int TotalSessionsCompleted { get; set; }

    public int CurrentLevel { get; set; } = 1; // Level pembelajaran

    [MaxLength(100)]
    public string? CurrentBadge { get; set; } = "Pemula";

    public int ExperiencePoints { get; set; } // XP untuk gamifikasi

    public bool HasPassedTheory { get; set; }

    public bool HasPassedPractical { get; set; }

    public DateTime? TheoryExamDate { get; set; }

    public DateTime? PracticalExamDate { get; set; }

    public DateTime EnrollmentDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? LearningNotes { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<StudentFeedback> Feedbacks { get; set; } = new List<StudentFeedback>();
    public ICollection<StudentBadge> Badges { get; set; } = new List<StudentBadge>();
    public ICollection<ExamAttempt> ExamAttempts { get; set; } = new List<ExamAttempt>();
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;
using SmartDrive.Models.Enums;

namespace SmartDrive.Models.Entities;

/// <summary>
/// Extended user entity dengan data tambahan untuk SmartDrive
/// </summary>
public class ApplicationUser : IdentityUser
{
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public Gender? Gender { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(50)]
    public string? IdCardNumber { get; set; }

    [MaxLength(50)]
    public string? SimNumber { get; set; }

    [MaxLength(255)]
    public string? IdCardFilePath { get; set; }

    [MaxLength(255)]
    public string? SimFilePath { get; set; }

    [MaxLength(255)]
    public string? ProfilePicturePath { get; set; }

    public UserRole Role { get; set; } = UserRole.Student;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public InstructorProfile? InstructorProfile { get; set; }
    public StudentProfile? StudentProfile { get; set; }
    public ICollection<ChatMessage> SentMessages { get; set; } = new List<ChatMessage>();
    public ICollection<ChatMessage> ReceivedMessages { get; set; } = new List<ChatMessage>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

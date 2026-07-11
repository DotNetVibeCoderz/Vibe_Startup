using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartDrive.Models.Enums;

namespace SmartDrive.Models.Entities;

/// <summary>
/// Feedback/penilaian dari instruktur untuk siswa
/// </summary>
public class StudentFeedback
{
    [Key]
    public int Id { get; set; }

    public int BookingId { get; set; }

    [ForeignKey(nameof(BookingId))]
    public Booking? Booking { get; set; }

    public int InstructorId { get; set; }

    [ForeignKey(nameof(InstructorId))]
    public InstructorProfile? Instructor { get; set; }

    public int StudentId { get; set; }

    [ForeignKey(nameof(StudentId))]
    public StudentProfile? Student { get; set; }

    public int Rating { get; set; } // 1-5

    [MaxLength(2000)]
    public string? Comments { get; set; }

    [MaxLength(500)]
    public string? Strengths { get; set; } // Kelebihan siswa

    [MaxLength(500)]
    public string? AreasForImprovement { get; set; } // Yang perlu diperbaiki

    [MaxLength(100)]
    public string? SkillsDemonstrated { get; set; } // CSV: Parking, Steering, etc.

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Badge/achievement siswa (gamifikasi)
/// </summary>
public class StudentBadge
{
    [Key]
    public int Id { get; set; }

    public int StudentId { get; set; }

    [ForeignKey(nameof(StudentId))]
    public StudentProfile? Student { get; set; }

    [Required, MaxLength(100)]
    public string BadgeName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public BadgeLevel Level { get; set; } = BadgeLevel.Bronze;

    [MaxLength(255)]
    public string? IconPath { get; set; }

    public DateTime AwardedAt { get; set; } = DateTime.UtcNow;

    public int XpAwarded { get; set; }
}

/// <summary>
/// Notifikasi untuk user
/// </summary>
public class Notification
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Message { get; set; }

    public NotificationType Type { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }

    [MaxLength(255)]
    public string? ActionUrl { get; set; }
}

/// <summary>
/// Chat message antara instruktur dan siswa
/// </summary>
public class ChatMessage
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string SenderId { get; set; } = string.Empty;

    [ForeignKey(nameof(SenderId))]
    public ApplicationUser? Sender { get; set; }

    [Required]
    public string ReceiverId { get; set; } = string.Empty;

    [ForeignKey(nameof(ReceiverId))]
    public ApplicationUser? Receiver { get; set; }

    [MaxLength(2000)]
    public string? MessageText { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [MaxLength(500)]
    public string? AttachmentUrl { get; set; }

    [MaxLength(255)]
    public string? AttachmentName { get; set; }

    [MaxLength(50)]
    public string? EmojiReaction { get; set; }

    public bool IsLiked { get; set; }

    public bool IsRead { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }
}

/// <summary>
/// Sesi chat bot AI (Om Bambang)
/// </summary>
public class ChatBotSession
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = "New Chat";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastActivityAt { get; set; }

    public ICollection<ChatBotMessage> Messages { get; set; } = new List<ChatBotMessage>();
}

/// <summary>
/// Pesan dalam sesi chat bot
/// </summary>
public class ChatBotMessage
{
    [Key]
    public int Id { get; set; }

    public int SessionId { get; set; }

    [ForeignKey(nameof(SessionId))]
    public ChatBotSession? Session { get; set; }

    [Required, MaxLength(20)]
    public string Role { get; set; } = "user"; // user, assistant, system

    public string Content { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrls { get; set; } // JSON array of image URLs

    [MaxLength(500)]
    public string? AttachmentUrls { get; set; } // JSON array

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public int? TokenCount { get; set; }

    [MaxLength(100)]
    public string? ModelUsed { get; set; }
}

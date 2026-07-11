using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartDrive.Models.Enums;

namespace SmartDrive.Models.Entities;

/// <summary>
/// Pembayaran untuk booking atau marketplace
/// </summary>
public class Payment
{
    [Key]
    public int Id { get; set; }

    public int? BookingId { get; set; }

    [ForeignKey(nameof(BookingId))]
    public Booking? Booking { get; set; }

    public int? OrderId { get; set; }

    [ForeignKey(nameof(OrderId))]
    public MarketplaceOrder? Order { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [MaxLength(100)]
    public string? TransactionId { get; set; }

    [MaxLength(50)]
    public string? PaymentReference { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(255)]
    public string? ProofFilePath { get; set; } // Bukti pembayaran
}

/// <summary>
/// Modul teori pembelajaran
/// </summary>
public class TheoryModule
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public string Content { get; set; } = string.Empty; // HTML/Markdown content

    public int OrderIndex { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; } // TrafficSigns, RoadRules, BasicTechnique, etc.

    [MaxLength(255)]
    public string? ImagePath { get; set; }

    [MaxLength(255)]
    public string? VideoUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<ExamQuestion> Questions { get; set; } = new List<ExamQuestion>();
}

/// <summary>
/// Soal ujian teori
/// </summary>
public class ExamQuestion
{
    [Key]
    public int Id { get; set; }

    public int? ModuleId { get; set; }

    [ForeignKey(nameof(ModuleId))]
    public TheoryModule? Module { get; set; }

    [Required]
    public string QuestionText { get; set; } = string.Empty;

    public string? ImagePath { get; set; }

    [Required]
    public string OptionA { get; set; } = string.Empty;

    public string OptionB { get; set; } = string.Empty;

    public string OptionC { get; set; } = string.Empty;

    public string OptionD { get; set; } = string.Empty;

    [MaxLength(1)]
    public string CorrectAnswer { get; set; } = "A"; // A, B, C, D

    [MaxLength(500)]
    public string? Explanation { get; set; }

    public int Difficulty { get; set; } = 1; // 1-5

    [MaxLength(50)]
    public string? Category { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Attempt ujian siswa
/// </summary>
public class ExamAttempt
{
    [Key]
    public int Id { get; set; }

    public int StudentId { get; set; }

    [ForeignKey(nameof(StudentId))]
    public StudentProfile? Student { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? FinishedAt { get; set; }

    public int TotalQuestions { get; set; }

    public int CorrectAnswers { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal Score { get; set; }

    public bool IsPassed { get; set; }

    public int PassingScore { get; set; } = 70; // Default 70%

    [MaxLength(2000)]
    public string? AnswersJson { get; set; } // JSON: question ID -> answer chosen
}

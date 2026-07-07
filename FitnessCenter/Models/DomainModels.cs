using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace FitnessCenter.Models;

// ============================
// IDENTITY & USERS
// ============================

/// <summary>Custom Application User extending Identity</summary>
public class ApplicationUser : IdentityUser
{
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Member;

    [MaxLength(50)]
    public string? KtpNumber { get; set; }

    public Gender Gender { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(200)]
    public string? ProfilePictureUrl { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public int LoyaltyPoints { get; set; }

    [MaxLength(100)]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20)]
    public string? EmergencyContactPhone { get; set; }

    public DateTime? MembershipExpiryDate { get; set; }

    // Navigation properties
    public ICollection<MemberMembership> MemberMemberships { get; set; } = new List<MemberMembership>();
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<ClassBooking> ClassBookings { get; set; } = new List<ClassBooking>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<ForumPost> ForumPosts { get; set; } = new List<ForumPost>();
    public ICollection<ForumComment> ForumComments { get; set; } = new List<ForumComment>();
    public ICollection<WorkoutLog> WorkoutLogs { get; set; } = new List<WorkoutLog>();
    public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    public ICollection<Achievement> Achievements { get; set; } = new List<Achievement>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
    public ICollection<EventRegistration> EventRegistrations { get; set; } = new List<EventRegistration>();
}

// ============================
// MEMBERSHIP
// ============================

public class MembershipPlan
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public MembershipDuration Duration { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountedPrice { get; set; }

    public bool AllowAutoRenew { get; set; }

    public int MaxClassesPerMonth { get; set; }

    public bool IncludesPersonalTrainer { get; set; }

    public bool IncludesNutritionPlan { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MemberMembership> MemberMemberships { get; set; } = new List<MemberMembership>();
}

public class MemberMembership
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int MembershipPlanId { get; set; }
    public MembershipPlan? MembershipPlan { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    public bool AutoRenew { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaid { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ============================
// ATTENDANCE
// ============================

public class Attendance
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public AttendanceType Type { get; set; } = AttendanceType.CheckIn;

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string? QrCodeData { get; set; }

    [MaxLength(100)]
    public string? DeviceInfo { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }
}

// ============================
// CLASSES & SCHEDULING
// ============================

public class FitnessClass
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public ClassType Type { get; set; }
    public ClassLevel Level { get; set; } = ClassLevel.AllLevels;

    public int TrainerId { get; set; }
    public Trainer? Trainer { get; set; }

    public int MaxParticipants { get; set; } = 20;

    [MaxLength(100)]
    public string? Room { get; set; }

    public TimeSpan Duration { get; set; } = TimeSpan.FromHours(1);

    public bool IsVirtual { get; set; }

    [MaxLength(500)]
    public string? VirtualLink { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(300)]
    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ClassSchedule> Schedules { get; set; } = new List<ClassSchedule>();
    public ICollection<ClassBooking> Bookings { get; set; } = new List<ClassBooking>();
}

public class ClassSchedule
{
    public int Id { get; set; }

    public int FitnessClassId { get; set; }
    public FitnessClass? FitnessClass { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;

    public DateTime? ValidUntil { get; set; }

    public bool IsCancelled { get; set; }

    public int CurrentBookings { get; set; }
}

public class ClassBooking
{
    public int Id { get; set; }

    public int ScheduleId { get; set; }
    public ClassSchedule? Schedule { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateTime BookedAt { get; set; } = DateTime.UtcNow;

    public bool IsAttended { get; set; }

    public bool IsCancelled { get; set; }
}

// ============================
// TRAINER
// ============================

public class Trainer
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Specialization { get; set; }

    [MaxLength(1000)]
    public string? Bio { get; set; }

    [MaxLength(200)]
    public string? PhotoUrl { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    public double Rating { get; set; }

    public int TotalClasses { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Certifications { get; set; }

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public ICollection<FitnessClass> Classes { get; set; } = new List<FitnessClass>();
}

// ============================
// PAYMENT
// ============================

public class Payment
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public PaymentMethod Method { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? TransactionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }

    public int? DiscountId { get; set; }
    public Discount? Discount { get; set; }
}

// ============================
// DISCOUNTS & PROMOTIONS
// ============================

public class Discount
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DiscountType Type { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Value { get; set; }

    public DiscountScope Scope { get; set; } = DiscountScope.All;

    public int? MaxUses { get; set; }

    public int CurrentUses { get; set; }

    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MinPurchase { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

// ============================
// FORUM
// ============================

public class ForumPost
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(5000)]
    public string Content { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public int Likes { get; set; }

    public bool IsPinned { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<ForumComment> Comments { get; set; } = new List<ForumComment>();
    public ICollection<ForumReaction> Reactions { get; set; } = new List<ForumReaction>();
}

public class ForumComment
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public ForumPost? Post { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public int Likes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ForumReaction> Reactions { get; set; } = new List<ForumReaction>();
}

public class ForumReaction
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int? PostId { get; set; }
    public ForumPost? Post { get; set; }

    public int? CommentId { get; set; }
    public ForumComment? Comment { get; set; }

    [MaxLength(50)]
    public string ReactionType { get; set; } = "like"; // like, love, haha, wow, sad, angry

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ============================
// WORKOUT & NUTRITION
// ============================

public class WorkoutLog
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [MaxLength(100)]
    public string ExerciseName { get; set; } = string.Empty;

    public int Sets { get; set; }

    public int Reps { get; set; }

    [Column(TypeName = "decimal(8,2)")]
    public decimal? Weight { get; set; } // in kg

    public int? DurationMinutes { get; set; }

    public int? CaloriesBurned { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? DeviceSource { get; set; } // Fitbit, Apple Watch, Manual
}

public class NutritionPlan
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int DailyCalories { get; set; }

    [MaxLength(100)]
    public string? Goal { get; set; } // Weight Loss, Muscle Gain, Maintenance

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class MealPlan
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int NutritionPlanId { get; set; }
    public NutritionPlan? NutritionPlan { get; set; }

    public DateTime Date { get; set; }

    [MaxLength(100)]
    public string MealType { get; set; } = string.Empty; // Breakfast, Lunch, Dinner, Snack

    [MaxLength(200)]
    public string FoodName { get; set; } = string.Empty;

    public int Calories { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

// ============================
// FEEDBACK & SURVEYS
// ============================

public class Feedback
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public FeedbackType Type { get; set; }

    public int? ReferenceId { get; set; } // TrainerId, ClassId, etc.

    public int Rating { get; set; } // 1-5

    [MaxLength(2000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ============================
// EVENTS
// ============================

public class Event
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(10000)]
    public string? Content { get; set; } // HTML from WYSIWYG

    [MaxLength(500)]
    public string? Summary { get; set; }

    [MaxLength(300)]
    public string? ImageUrl { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Draft;

    public DateTime? EventDate { get; set; }

    [MaxLength(200)]
    public string? Location { get; set; }

    public int MaxParticipants { get; set; }

    public int CurrentParticipants { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Fee { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PublishedAt { get; set; }

    public int Likes { get; set; }

    public ICollection<EventRegistration> Registrations { get; set; } = new List<EventRegistration>();
    public ICollection<EventComment> Comments { get; set; } = new List<EventComment>();
}

public class EventRegistration
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event? Event { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public bool IsAttended { get; set; }
}

public class EventComment
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event? Event { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    public int Likes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ============================
// GAMIFICATION
// ============================

public class Achievement
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public AchievementCategory Category { get; set; }

    [MaxLength(200)]
    public string? BadgeIconUrl { get; set; }

    public int Points { get; set; }

    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
}

public class LeaderboardEntry
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public int TotalPoints { get; set; }
    public int Rank { get; set; }
    public string? ProfilePictureUrl { get; set; }
}

// ============================
// NOTIFICATIONS
// ============================

public class Notification
{
    public int Id { get; set; }

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Message { get; set; }

    public NotificationType Type { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(300)]
    public string? ActionUrl { get; set; }
}

// ============================
// CHAT BOT
// ============================

public class ChatSession
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Title { get; set; } = "New Chat";

    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public class ChatMessage
{
    public int Id { get; set; }

    public int SessionId { get; set; }
    public ChatSession? Session { get; set; }

    [MaxLength(50)]
    public string Role { get; set; } = "user"; // user, assistant, system

    [MaxLength(10000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    [MaxLength(1000)]
    public string? DocumentUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? ModelUsed { get; set; }
}

// ============================
// APP SETTINGS (DATABASE CONFIG)
// ============================

public class AppConfiguration
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Value { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    public bool IsEditable { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

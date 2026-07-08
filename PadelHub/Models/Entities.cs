using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PadelHub.Models;

/// <summary>
/// User entity extending ASP.NET Identity
/// </summary>
public class ApplicationUser : Microsoft.AspNetCore.Identity.IdentityUser
{
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    [MaxLength(50)]
    public string? MemberNumber { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(20)]
    public string? Gender { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? PhoneNumber2 { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public PlayerProfile? PlayerProfile { get; set; }
    public Coach? Coach { get; set; }
    public ICollection<UserMembership> Memberships { get; set; } = new List<UserMembership>();
    public ICollection<LoyaltyPoint> LoyaltyPoints { get; set; } = new List<LoyaltyPoint>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}

/// <summary>
/// Club entity
/// </summary>
public class Club
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    [MaxLength(500)]
    public string? BannerUrl { get; set; }

    [MaxLength(300)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Country { get; set; }

    [MaxLength(50)]
    public string? PostalCode { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    [MaxLength(100)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? Website { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Court> Courts { get; set; } = new List<Court>();
    public ICollection<Facility> Facilities { get; set; } = new List<Facility>();
    public ICollection<OperatingHour> OperatingHours { get; set; } = new List<OperatingHour>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

/// <summary>
/// Court entity
/// </summary>
public class Court
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public int ClubId { get; set; }
    public Club? Club { get; set; }

    [MaxLength(100)]
    public string? SurfaceType { get; set; } // Artificial Grass, Concrete, etc.

    [MaxLength(50)]
    public string? Type { get; set; } // Indoor, Outdoor

    public bool HasLighting { get; set; }
    public bool HasRoof { get; set; }
    public bool IsActive { get; set; } = true;

    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerHour { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PeakPricePerHour { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<CourtMaintenance> Maintenances { get; set; } = new List<CourtMaintenance>();
}

/// <summary>
/// Facility entity
/// </summary>
public class Facility
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public int ClubId { get; set; }
    public Club? Club { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? Icon { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Operating hours for clubs/courts
/// </summary>
public class OperatingHour
{
    public int Id { get; set; }

    public int ClubId { get; set; }
    public Club? Club { get; set; }

    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan OpenTime { get; set; }
    public TimeSpan CloseTime { get; set; }
    public bool IsClosed { get; set; }
}

/// <summary>
/// Reservation entity
/// </summary>
public class Reservation
{
    public int Id { get; set; }

    public int CourtId { get; set; }
    public Court? Court { get; set; }

    public int? ClubId { get; set; }
    public Club? Club { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateTime ReservationDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Cancelled, Completed

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }

    // Navigation
    public Payment? Payment { get; set; }
}

/// <summary>
/// Payment entity
/// </summary>
public class Payment
{
    public int Id { get; set; }

    public int ReservationId { get; set; }
    public Reservation? Reservation { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string PaymentMethod { get; set; } = string.Empty; // EWallet, CreditCard, BankTransfer

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Success, Failed, Refunded

    [MaxLength(200)]
    public string? TransactionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

/// <summary>
/// Membership package entity
/// </summary>
public class MembershipPackage
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string Type { get; set; } = "Monthly"; // Monthly, Yearly

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? DiscountPercent { get; set; }

    public int DurationDays { get; set; }
    public int MaxReservationsPerMonth { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<UserMembership> UserMemberships { get; set; } = new List<UserMembership>();
}

/// <summary>
/// User membership entity
/// </summary>
public class UserMembership
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int PackageId { get; set; }
    public MembershipPackage? Package { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Active"; // Active, Expired, Cancelled

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountPaid { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Loyalty point entity
/// </summary>
public class LoyaltyPoint
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int Points { get; set; }
    public int PointsEarned { get; set; }
    public int PointsRedeemed { get; set; }

    [MaxLength(200)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Player profile entity
/// </summary>
public class PlayerProfile
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int? Ranking { get; set; }
    public int? Rating { get; set; }

    [MaxLength(50)]
    public string? Level { get; set; } // Beginner, Intermediate, Advanced, Professional

    [MaxLength(50)]
    public string? PlayingStyle { get; set; }

    [MaxLength(50)]
    public string? DominantHand { get; set; } // Right, Left

    public int TotalMatches { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<PlayerStat> Stats { get; set; } = new List<PlayerStat>();
    public ICollection<PlayerAchievement> Achievements { get; set; } = new List<PlayerAchievement>();
    public ICollection<MatchPlayer> MatchPlayers { get; set; } = new List<MatchPlayer>();
}

/// <summary>
/// Player statistics entity
/// </summary>
public class PlayerStat
{
    public int Id { get; set; }

    public int PlayerProfileId { get; set; }
    public PlayerProfile? PlayerProfile { get; set; }

    public int? Aces { get; set; }
    public int? DoubleFaults { get; set; }
    public int? Winners { get; set; }
    public int? UnforcedErrors { get; set; }
    public int? FirstServePercentage { get; set; }
    public int? PointsWon { get; set; }
    public int? GamesWon { get; set; }

    [MaxLength(500)]
    public string? HeatmapData { get; set; } // JSON heatmap data

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Player achievement entity
/// </summary>
public class PlayerAchievement
{
    public int Id { get; set; }

    public int PlayerProfileId { get; set; }
    public PlayerProfile? PlayerProfile { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Type { get; set; } // Badge, Trophy, Medal

    [MaxLength(500)]
    public string? IconUrl { get; set; }

    public DateTime AchievedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Coach entity
/// </summary>
public class Coach
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public int? ClubId { get; set; }
    public Club? Club { get; set; }

    [MaxLength(500)]
    public string? Bio { get; set; }

    [MaxLength(200)]
    public string? Specialization { get; set; }

    public int? ExperienceYears { get; set; }

    [MaxLength(500)]
    public string? Certifications { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal HourlyRate { get; set; }

    public bool IsAvailable { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<TrainingSession> TrainingSessions { get; set; } = new List<TrainingSession>();
    public ICollection<CourseMaterial> CourseMaterials { get; set; } = new List<CourseMaterial>();
}

/// <summary>
/// Training session entity
/// </summary>
public class TrainingSession
{
    public int Id { get; set; }

    public int CoachId { get; set; }
    public Coach? Coach { get; set; }

    [Required, MaxLength(450)]
    public string StudentId { get; set; } = string.Empty;
    public ApplicationUser? Student { get; set; }

    public int? CourtId { get; set; }
    public Court? Court { get; set; }

    public DateTime SessionDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Scheduled"; // Scheduled, InProgress, Completed, Cancelled

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? Feedback { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Course material entity
/// </summary>
public class CourseMaterial
{
    public int Id { get; set; }

    public int CoachId { get; set; }
    public Coach? Coach { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; } // Technique, Strategy, Fitness, Mental

    [MaxLength(500)]
    public string? FileUrl { get; set; }

    [MaxLength(500)]
    public string? VideoUrl { get; set; }

    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Tournament entity
/// </summary>
public class Tournament
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public int? ClubId { get; set; }
    public Club? Club { get; set; }

    [MaxLength(50)]
    public string Type { get; set; } = "Single"; // Single, Double, Mixed

    [MaxLength(50)]
    public string Format { get; set; } = "Knockout"; // Knockout, RoundRobin, GroupStage

    [MaxLength(50)]
    public string? Level { get; set; } // Open, Beginner, Intermediate, Advanced, Professional

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime RegistrationDeadline { get; set; }

    public int MaxParticipants { get; set; }
    public int? MaxTeams { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal EntryFee { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PrizeMoney { get; set; }

    [MaxLength(500)]
    public string? BannerUrl { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Upcoming"; // Upcoming, Registration, InProgress, Completed, Cancelled

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<TournamentRegistration> Registrations { get; set; } = new List<TournamentRegistration>();
    public ICollection<Match> Matches { get; set; } = new List<Match>();
}

/// <summary>
/// Tournament registration entity
/// </summary>
public class TournamentRegistration
{
    public int Id { get; set; }

    public int TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [MaxLength(200)]
    public string? TeamName { get; set; }

    [MaxLength(450)]
    public string? PartnerUserId { get; set; }
    public ApplicationUser? PartnerUser { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Confirmed, Rejected

    public int? Seed { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Match entity
/// </summary>
public class Match
{
    public int Id { get; set; }

    public int TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public int? CourtId { get; set; }
    public Court? Court { get; set; }

    public int Round { get; set; }

    [MaxLength(200)]
    public string? RoundName { get; set; } // Final, SemiFinal, QuarterFinal, etc.

    public DateTime? ScheduledTime { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Scheduled"; // Scheduled, InProgress, Completed, Cancelled

    public int? WinnerId { get; set; }

    [MaxLength(1000)]
    public string? Score { get; set; } // JSON score data

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<MatchPlayer> MatchPlayers { get; set; } = new List<MatchPlayer>();
}

/// <summary>
/// Match player entity (many-to-many)
/// </summary>
public class MatchPlayer
{
    public int Id { get; set; }

    public int MatchId { get; set; }
    public Match? Match { get; set; }

    public int PlayerProfileId { get; set; }
    public PlayerProfile? PlayerProfile { get; set; }

    [MaxLength(50)]
    public string Team { get; set; } = "A"; // A or B team

    public bool IsWinner { get; set; }

    [MaxLength(500)]
    public string? IndividualScore { get; set; }
}

/// <summary>
/// Timeline post for social features
/// </summary>
public class TimelinePost
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [MaxLength(2000)]
    public string? Content { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [MaxLength(500)]
    public string? VideoUrl { get; set; }

    [MaxLength(50)]
    public string? PostType { get; set; } // MatchResult, Highlight, General, Event

    public int? MatchId { get; set; }
    public Match? Match { get; set; }

    public int LikesCount { get; set; }
    public int CommentsCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<TimelineComment> Comments { get; set; } = new List<TimelineComment>();
    public ICollection<TimelineLike> Likes { get; set; } = new List<TimelineLike>();
}

/// <summary>
/// Timeline comment entity
/// </summary>
public class TimelineComment
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public TimelinePost? Post { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Timeline like entity
/// </summary>
public class TimelineLike
{
    public int Id { get; set; }

    public int PostId { get; set; }
    public TimelinePost? Post { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [MaxLength(50)]
    public string? EmojiType { get; set; } // Like, Love, Haha, Wow, Sad, Angry, Fire, Trophy

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Chat message entity
/// </summary>
public class ChatMessage
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser? Sender { get; set; }

    [MaxLength(450)]
    public string? ReceiverId { get; set; }
    public ApplicationUser? Receiver { get; set; }

    public int? GroupId { get; set; }
    public ChatGroup? Group { get; set; }

    [MaxLength(5000)]
    public string? Content { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [MaxLength(500)]
    public string? FileUrl { get; set; }

    [MaxLength(200)]
    public string? FileName { get; set; }

    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

/// <summary>
/// Chat group entity
/// </summary>
public class ChatGroup
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ChatGroupMember> Members { get; set; } = new List<ChatGroupMember>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

/// <summary>
/// Chat group member entity
/// </summary>
public class ChatGroupMember
{
    public int Id { get; set; }

    public int GroupId { get; set; }
    public ChatGroup? Group { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [MaxLength(50)]
    public string Role { get; set; } = "Member"; // Admin, Member

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Forum topic entity
/// </summary>
public class ForumTopic
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Content { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [Required, MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser? CreatedByUser { get; set; }

    public int ViewCount { get; set; }
    public int ReplyCount { get; set; }
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastReplyAt { get; set; }

    // Navigation
    public ICollection<ForumPost> Posts { get; set; } = new List<ForumPost>();
}

/// <summary>
/// Forum post (reply) entity
/// </summary>
public class ForumPost
{
    public int Id { get; set; }

    public int TopicId { get; set; }
    public ForumTopic? Topic { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    [Required, MaxLength(5000)]
    public string Content { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Social event entity
/// </summary>
public class SocialEvent
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string Type { get; set; } = "Gathering"; // Gathering, FunMatch, Charity, Tournament

    public int? ClubId { get; set; }
    public Club? Club { get; set; }

    public DateTime EventDate { get; set; }
    public DateTime? EndDate { get; set; }

    [MaxLength(500)]
    public string? Location { get; set; }

    public int? MaxParticipants { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Fee { get; set; }

    [MaxLength(500)]
    public string? BannerUrl { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Upcoming";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Court maintenance entity
/// </summary>
public class CourtMaintenance
{
    public int Id { get; set; }

    public int CourtId { get; set; }
    public Court? Court { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Scheduled"; // Scheduled, InProgress, Completed

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Audit log entity
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    [MaxLength(450)]
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }

    [MaxLength(200)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? EntityName { get; set; }

    [MaxLength(1000)]
    public string? EntityId { get; set; }

    [MaxLength(5000)]
    public string? Details { get; set; }

    [MaxLength(100)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Badge entity for gamification
/// </summary>
public class Badge
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? IconUrl { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public int PointsRequired { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
}

/// <summary>
/// User badge entity
/// </summary>
public class UserBadge
{
    public int Id { get; set; }

    public int BadgeId { get; set; }
    public Badge? Badge { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// IoT sensor data entity
/// </summary>
public class SensorData
{
    public long Id { get; set; }

    public int CourtId { get; set; }
    public Court? Court { get; set; }

    [MaxLength(100)]
    public string SensorType { get; set; } = string.Empty; // Temperature, Humidity, Lighting, BallTracking

    [MaxLength(1000)]
    public string? Value { get; set; } // JSON value

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// IoT simulator configuration
/// </summary>
public class IoTSimulator
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string SensorType { get; set; } = string.Empty;

    public int IntervalMs { get; set; } = 5000;
    public bool IsRunning { get; set; }

    [MaxLength(500)]
    public string? Parameters { get; set; } // JSON parameters

    public DateTime? StartedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
}

/// <summary>
/// System configuration that can be changed from UI
/// </summary>
public class SystemConfig
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string? Value { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

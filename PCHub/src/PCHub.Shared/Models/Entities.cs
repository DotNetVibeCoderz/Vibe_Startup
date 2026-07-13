using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PCHub.Shared.Enums;

namespace PCHub.Shared.Models;

/// <summary>User / Member entity</summary>
public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    public UserRole Role { get; set; } = UserRole.Member;

    public MembershipTier MembershipTier { get; set; } = MembershipTier.Basic;

    public int LoyaltyPoints { get; set; } = 0;

    public decimal Balance { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    [MaxLength(50)]
    public string? ResetToken { get; set; }

    public DateTime? ResetTokenExpiry { get; set; }

    // Navigation
    public List<BillingSession> BillingSessions { get; set; } = [];
    public List<Reservation> Reservations { get; set; } = [];
    public List<SupportTicket> SupportTickets { get; set; } = [];
}

/// <summary>Komputer / PC yang disewakan</summary>
public class Pc
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string PcNumber { get; set; } = string.Empty;

    public PcStatus Status { get; set; } = PcStatus.Available;

    [MaxLength(500)]
    public string Specifications { get; set; } = string.Empty;

    public decimal HourlyRate { get; set; } = 5000;

    public string? CurrentUserId { get; set; }

    public DateTime? LastMaintenanceAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Monitoring
    public double? CpuUsage { get; set; }
    public double? GpuUsage { get; set; }
    public double? RamUsage { get; set; }

    public List<PcSession> Sessions { get; set; } = [];
}

/// <summary>Game yang tersedia</summary>
public class Game
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public GameGenre Genre { get; set; } = GameGenre.Other;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? ExecutablePath { get; set; }

    [MaxLength(500)]
    public string? IconUrl { get; set; }

    [MaxLength(500)]
    public string? CoverImageUrl { get; set; }

    public bool IsInstalled { get; set; } = true;

    public string? LicenseInfo { get; set; }

    [MaxLength(50)]
    public string? Version { get; set; }

    public bool IsPopular { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<PcSession> Sessions { get; set; } = [];
}

/// <summary>Sesi billing pemakaian PC</summary>
public class BillingSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid PcId { get; set; }
    public Pc? Pc { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    public DateTime? EndTime { get; set; }

    public decimal HourlyRate { get; set; }

    public decimal TotalCost { get; set; } = 0;

    public BillingStatus Status { get; set; } = BillingStatus.Active;

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Reservasi / booking PC</summary>
public class Reservation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid? PcId { get; set; }
    public Pc? Pc { get; set; }

    public DateTime ReservationDate { get; set; }

    public int DurationMinutes { get; set; } = 60;

    public string? GameRequested { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Sesi pemakaian PC dengan game</summary>
public class PcSession
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PcId { get; set; }
    public Pc? Pc { get; set; }

    public Guid? GameId { get; set; }
    public Game? Game { get; set; }

    public Guid UserId { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    public DateTime? EndTime { get; set; }

    public int DurationMinutes { get; set; }
}

/// <summary>Membership / paket langganan</summary>
public class Membership
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public MembershipTier Tier { get; set; } = MembershipTier.Basic;

    [MaxLength(500)]
    public string? Description { get; set; }

    public decimal MonthlyPrice { get; set; } = 0;

    public int DiscountPercentage { get; set; } = 0;

    public int BonusHours { get; set; } = 0;

    public int LoyaltyPointsPerMonth { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<UserMembership> UserMemberships { get; set; } = [];
}

/// <summary>Relasi user-membership</summary>
public class UserMembership
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid MembershipId { get; set; }
    public Membership? Membership { get; set; }

    public DateTime StartDate { get; set; } = DateTime.UtcNow;

    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>Promo / diskon</summary>
public class Promo
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string? PromoCode { get; set; }

    public int DiscountPercentage { get; set; } = 0;

    public decimal? MaxDiscount { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Support ticket dari user</summary>
public class SupportTicket
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User? User { get; set; }

    [Required, MaxLength(500)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Status { get; set; } = "Open"; // Open, InProgress, Resolved, Closed

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }

    public List<SupportReply> Replies { get; set; } = [];
}

/// <summary>Balasan support ticket</summary>
public class SupportReply
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TicketId { get; set; }
    public SupportTicket? Ticket { get; set; }

    public Guid UserId { get; set; }

    [Required]
    public string Message { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Notifikasi</summary>
public class Notification
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }

    [Required, MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Message { get; set; } = string.Empty;

    public NotificationType Type { get; set; } = NotificationType.System;

    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? SentAt { get; set; }
}

/// <summary>Laporan keuangan</summary>
public class FinancialReport
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime ReportDate { get; set; }

    public decimal TotalRevenue { get; set; }

    public decimal TotalBilling { get; set; }

    public decimal TotalMembership { get; set; }

    public int TotalSessions { get; set; }

    public int TotalActiveUsers { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Event / Turnamen</summary>
public class Tournament
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public Guid? GameId { get; set; }
    public Game? Game { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public int MaxParticipants { get; set; } = 32;

    public decimal EntryFee { get; set; } = 0;

    public decimal PrizePool { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<TournamentParticipant> Participants { get; set; } = [];
}

/// <summary>Peserta turnamen</summary>
public class TournamentParticipant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TournamentId { get; set; }
    public Tournament? Tournament { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public int? Rank { get; set; }

    public decimal? PrizeWon { get; set; }
}

/// <summary>Log aktivitas sistem</summary>
public class ActivityLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? UserId { get; set; }

    [MaxLength(200)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Details { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Konfigurasi sistem</summary>
public class SystemConfig
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

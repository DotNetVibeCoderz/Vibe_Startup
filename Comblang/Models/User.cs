using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Comblang.Models;

/// <summary>
/// Represents a registered user in the Comblang dating application.
/// </summary>
public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100), EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Role { get; set; } = "User";

    public bool IsVerified { get; set; }
    public bool IsPremium { get; set; }
    public bool IsBanned { get; set; }

    // ── Password Reset ──
    [MaxLength(200)]
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }

    // ── Location ──
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    [MaxLength(200)] public string? City { get; set; }
    [MaxLength(100)] public string? Country { get; set; }
    public bool TravelMode { get; set; }
    public double TravelLatitude { get; set; }
    public double TravelLongitude { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Profile? Profile { get; set; }
    public ICollection<Match> Matches { get; set; } = new List<Match>();
    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<InterestTag> InterestTags { get; set; } = new List<InterestTag>();
    public ICollection<Swipe> Swipes { get; set; } = new List<Swipe>();
    public ICollection<Boost> Boosts { get; set; } = new List<Boost>();
    public ICollection<Report> Reports { get; set; } = new List<Report>();
    public ICollection<UserBlock> BlockedUsers { get; set; } = new List<UserBlock>();
    public ICollection<UserBlock> BlockedByUsers { get; set; } = new List<UserBlock>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<GiftTransaction> GiftTransactionsSent { get; set; } = new List<GiftTransaction>();
    public ICollection<GiftTransaction> GiftTransactionsReceived { get; set; } = new List<GiftTransaction>();
}

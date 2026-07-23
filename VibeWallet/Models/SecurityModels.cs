using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeWallet.Models;

/// <summary>
/// OTP code for transaction verification
/// </summary>
public class OtpCode : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Purpose { get; set; } = string.Empty; // "login", "transaction", "pin_reset"

    [MaxLength(50)]
    public string? TransactionRef { get; set; }

    [MaxLength(50)]
    public string Channel { get; set; } = "sms"; // sms, email

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public DateTime? UsedAt { get; set; }

    public int AttemptCount { get; set; } = 0;
}

/// <summary>
/// Fraud detection alert
/// </summary>
public class FraudAlert : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [MaxLength(50)]
    public string? TransactionRef { get; set; }

    public FraudAlertLevel AlertLevel { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? TriggerReason { get; set; }

    public bool IsResolved { get; set; } = false;

    public DateTime? ResolvedAt { get; set; }

    [MaxLength(500)]
    public string? Resolution { get; set; }

    [MaxLength(100)]
    public string? ResolvedBy { get; set; }
}

/// <summary>
/// Security log for audit trail
/// </summary>
public class SecurityLog : BaseEntity
{
    public Guid? UserId { get; set; }

    [MaxLength(200)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [MaxLength(500)]
    public string? Details { get; set; }

    public bool IsSuspicious { get; set; } = false;
}

/// <summary>
/// Login attempt tracking
/// </summary>
public class LoginAttempt : BaseEntity
{
    [MaxLength(200)]
    public string? Username { get; set; }

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    public bool IsSuccess { get; set; }

    [MaxLength(500)]
    public string? FailureReason { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeWallet.Models;

/// <summary>
/// KYC Document uploaded by user
/// </summary>
public class KycDocument : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    public IdentityType DocumentType { get; set; }

    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FileUrl { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public KycStatus Status { get; set; } = KycStatus.Submitted;

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public DateTime? VerifiedAt { get; set; }

    [MaxLength(100)]
    public string? VerifiedBy { get; set; }
}

/// <summary>
/// Selfie photo for KYC verification
/// </summary>
public class KycSelfie : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [MaxLength(200)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string FileUrl { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public KycStatus Status { get; set; } = KycStatus.Submitted;

    [MaxLength(500)]
    public string? RejectionReason { get; set; }
}

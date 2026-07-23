using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeWallet.Models;

/// <summary>
/// Bank account linked to user
/// </summary>
public class BankAccount : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [MaxLength(100)]
    public string BankName { get; set; } = string.Empty;

    [MaxLength(10)]
    public string BankCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string AccountNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string AccountHolderName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? BranchCode { get; set; }

    public bool IsVerified { get; set; } = false;
    public bool IsPrimary { get; set; } = false;

    public DateTime? VerifiedAt { get; set; }

    [MaxLength(200)]
    public string? BankLogoUrl { get; set; }
}

/// <summary>
/// Supported bank for integration
/// </summary>
public class SupportedBank : BaseEntity
{
    [MaxLength(100)]
    public string BankName { get; set; } = string.Empty;

    [MaxLength(10)]
    public string BankCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? SwiftCode { get; set; }

    [MaxLength(200)]
    public string? LogoUrl { get; set; }

    public bool IsActive { get; set; } = true;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TransferFee { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AdminFee { get; set; } = 2500;

    public int SortOrder { get; set; } = 0;
}

/// <summary>
/// Bank transfer transaction
/// </summary>
public class BankTransfer : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [MaxLength(50)]
    public string TransferRef { get; set; } = string.Empty;

    [MaxLength(100)]
    public string DestinationBank { get; set; } = string.Empty;

    [MaxLength(10)]
    public string DestinationBankCode { get; set; } = string.Empty;

    [MaxLength(50)]
    public string DestinationAccountNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string DestinationAccountName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Fee { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount => Amount + Fee;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public DateTime? CompletedAt { get; set; }
}

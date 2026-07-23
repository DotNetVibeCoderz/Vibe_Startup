using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeWallet.Models;

/// <summary>
/// User wallet containing balance and account information
/// </summary>
public class Wallet : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    /// <summary>
    /// Unique wallet number (like account number)
    /// </summary>
    [MaxLength(16)]
    public string WalletNumber { get; set; } = string.Empty;

    /// <summary>
    /// Current balance in IDR (Rupiah)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; } = 0;

    /// <summary>
    /// Amount on hold (pending transactions)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal HoldBalance { get; set; } = 0;

    /// <summary>
    /// Available balance = Balance - HoldBalance
    /// </summary>
    [NotMapped]
    public decimal AvailableBalance => Balance - HoldBalance;

    /// <summary>
    /// Total lifetime top-up
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalTopUp { get; set; } = 0;

    /// <summary>
    /// Total lifetime spending
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalSpending { get; set; } = 0;

    /// <summary>
    /// Loyalty points
    /// </summary>
    public int LoyaltyPoints { get; set; } = 0;

    /// <summary>
    /// Wallet status
    /// </summary>
    public bool IsActive { get; set; } = true;
    public bool IsFrozen { get; set; } = false;

    [MaxLength(500)]
    public string? FreezeReason { get; set; }

    /// <summary>
    /// Daily transaction tracking
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal DailyTransferAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DailyPaymentAmount { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DailyTopUpAmount { get; set; } = 0;

    public DateTime DailyLimitResetAt { get; set; } = DateTime.UtcNow.Date.AddDays(1);

    /// <summary>
    /// Monthly transaction tracking
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyTransferAmount { get; set; } = 0;

    public DateTime MonthlyLimitResetAt { get; set; } = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1);

    // Navigation
    public virtual ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}

/// <summary>
/// Wallet transaction record
/// </summary>
public class WalletTransaction : BaseEntity
{
    [Required]
    public Guid WalletId { get; set; }

    [ForeignKey(nameof(WalletId))]
    public virtual Wallet? Wallet { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    /// <summary>
    /// Unique transaction reference number
    /// </summary>
    [MaxLength(50)]
    public string TransactionRef { get; set; } = string.Empty;

    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public PaymentMethod Method { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Fee { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount => Amount + Fee;

    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceBefore { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceAfter { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? CounterpartyName { get; set; }

    [MaxLength(50)]
    public string? CounterpartyWallet { get; set; }

    [MaxLength(200)]
    public string? Notes { get; set; }

    public DateTime? CompletedAt { get; set; }

    // Metadata
    public string? MetadataJson { get; set; }
}

/// <summary>
/// Daily balance snapshot for analytics
/// </summary>
public class BalanceHistory : BaseEntity
{
    public Guid WalletId { get; set; }

    [ForeignKey(nameof(WalletId))]
    public virtual Wallet? Wallet { get; set; }

    public DateTime SnapshotDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal OpeningBalance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ClosingBalance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalIn { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalOut { get; set; }

    public int TransactionCount { get; set; }
}

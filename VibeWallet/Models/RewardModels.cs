using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeWallet.Models;

/// <summary>
/// Cashback record
/// </summary>
public class Cashback : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [MaxLength(50)]
    public string? TransactionRef { get; set; }

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,5)")]
    public decimal CashbackRate { get; set; } // e.g., 0.005 = 0.5%

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Completed;

    public DateTime? CreditedAt { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public bool IsRedeemed { get; set; } = false;
}

/// <summary>
/// Loyalty point transaction
/// </summary>
public class LoyaltyPoint : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [MaxLength(50)]
    public string? TransactionRef { get; set; }

    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    public int Points { get; set; } // Positive for earning, negative for redeeming

    [MaxLength(50)]
    public string? PointSource { get; set; } // "transaction", "promo", "bonus", "redemption"

    public DateTime? ExpiryDate { get; set; }

    public bool IsExpired { get; set; } = false;
}

/// <summary>
/// Voucher template
/// </summary>
public class Voucher : BaseEntity
{
    [MaxLength(100)]
    public string VoucherCode { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public PromoType VoucherType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Value { get; set; } // Percentage or fixed amount

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MinimumTransaction { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaximumDiscount { get; set; }

    public int TotalQuota { get; set; }
    public int UsedQuota { get; set; } = 0;

    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }

    public bool IsActive { get; set; } = true;

    public int PointsRequired { get; set; } = 0;

    [MaxLength(200)]
    public string? ImageUrl { get; set; }
}

/// <summary>
/// Voucher claimed by user
/// </summary>
public class UserVoucher : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [Required]
    public Guid VoucherId { get; set; }

    [ForeignKey(nameof(VoucherId))]
    public virtual Voucher? Voucher { get; set; }

    public bool IsRedeemed { get; set; } = false;

    [MaxLength(50)]
    public string? RedeemedTransactionRef { get; set; }

    public DateTime? RedeemedAt { get; set; }

    public DateTime ClaimedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Promo / discount
/// </summary>
public class Promo : BaseEntity
{
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public PromoType Type { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Value { get; set; }

    [MaxLength(100)]
    public string? MerchantName { get; set; }

    [MaxLength(200)]
    public string? MerchantLogoUrl { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [MaxLength(500)]
    public string? TermsAndConditions { get; set; }

    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }

    public bool IsActive { get; set; } = true;

    public int Priority { get; set; } = 0;

    [MaxLength(50)]
    public string? Category { get; set; } // "food", "transport", "shopping", etc.
}

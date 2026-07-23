using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VibeWallet.Models;

/// <summary>
/// QRIS Payment transaction
/// </summary>
public class QrisPayment : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [MaxLength(50)]
    public string PaymentRef { get; set; } = string.Empty;

    [MaxLength(500)]
    public string QrContent { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? MerchantName { get; set; }

    [MaxLength(100)]
    public string? MerchantId { get; set; }

    [MaxLength(50)]
    public string? TerminalId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TipAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount => Amount + (TipAmount ?? 0);

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public DateTime? PaidAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public string? QrImageUrl { get; set; }
}

/// <summary>
/// Bill payment transaction
/// </summary>
public class BillPayment : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [MaxLength(50)]
    public string PaymentRef { get; set; } = string.Empty;

    public BillType BillType { get; set; }

    [MaxLength(100)]
    public string ProviderName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string CustomerId { get; set; } = string.Empty; // No pelanggan

    [MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string BillPeriod { get; set; } = string.Empty; // e.g., "202501"

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AdminFee { get; set; } = 2500;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount => Amount + AdminFee;

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public DateTime? DueDate { get; set; }
    public DateTime? PaidAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Mobile top-up transaction
/// </summary>
public class MobileTopUp : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [MaxLength(50)]
    public string TopUpRef { get; set; } = string.Empty;

    public TopUpType Type { get; set; }

    public ProviderType Provider { get; set; }

    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ProductCode { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AdminFee { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount => Amount + AdminFee;

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public DateTime? CompletedAt { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// E-commerce / marketplace payment
/// </summary>
public class EcommercePayment : BaseEntity
{
    [Required]
    public Guid UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual VibeUser? User { get; set; }

    [MaxLength(50)]
    public string PaymentRef { get; set; } = string.Empty;

    [MaxLength(100)]
    public string PlatformName { get; set; } = string.Empty; // Tokopedia, Shopee, etc.

    [MaxLength(200)]
    public string OrderId { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(500)]
    public string? OrderDetails { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public DateTime? CompletedAt { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EstateHub.Models;

/// <summary>
/// Built-in chat messaging system
/// </summary>
public class ChatMessage
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string SenderId { get; set; } = string.Empty;

    [ForeignKey(nameof(SenderId))]
    public ApplicationUser? Sender { get; set; }

    [Required]
    public string ReceiverId { get; set; } = string.Empty;

    [ForeignKey(nameof(ReceiverId))]
    public ApplicationUser? Receiver { get; set; }

    [MaxLength(3000)]
    public string? TextContent { get; set; }

    /// <summary>text, image, document, emoji, system</summary>
    [MaxLength(20)]
    public string MessageType { get; set; } = "text";

    [MaxLength(500)]
    public string? AttachmentUrl { get; set; }

    [MaxLength(200)]
    public string? AttachmentName { get; set; }

    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public int? PropertyId { get; set; } // Optional: related property context
}

/// <summary>
/// Payment transaction record
/// </summary>
public class Payment
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    public int? PropertyId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>BookingFee, DownPayment, Installment, FullPayment, Advertising</summary>
    [MaxLength(30)]
    public string PaymentType { get; set; } = "FullPayment";

    /// <summary>E-Wallet, BankTransfer, VirtualAccount, CreditCard</summary>
    [MaxLength(30)]
    public string PaymentMethod { get; set; } = "BankTransfer";

    /// <summary>Pending, Completed, Failed, Refunded</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [MaxLength(100)]
    public string? TransactionId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Digital contract for sale/rent
/// </summary>
public class Contract
{
    [Key]
    public long Id { get; set; }

    public int PropertyId { get; set; }

    [ForeignKey(nameof(PropertyId))]
    public Property? Property { get; set; }

    [Required]
    public string BuyerId { get; set; } = string.Empty;

    [ForeignKey(nameof(BuyerId))]
    public ApplicationUser? Buyer { get; set; }

    [Required]
    public string SellerId { get; set; } = string.Empty;

    [ForeignKey(nameof(SellerId))]
    public ApplicationUser? Seller { get; set; }

    [MaxLength(50)]
    public string ContractType { get; set; } = "Sale"; // Sale, Rent

    public decimal Price { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; } // For rent

    public string? ContractContent { get; set; } // Full contract text/markdown

    [MaxLength(500)]
    public string? SignedDocumentUrl { get; set; }

    public DateTime? SignedAt { get; set; }

    /// <summary>Draft, Signed, Active, Completed, Cancelled</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Draft";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Real-time notification
/// </summary>
public class Notification
{
    [Key]
    public long Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Message { get; set; }

    /// <summary>Booking, Payment, Contract, System, Promotion</summary>
    [MaxLength(30)]
    public string Type { get; set; } = "System";

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(200)]
    public string? ActionUrl { get; set; }
}

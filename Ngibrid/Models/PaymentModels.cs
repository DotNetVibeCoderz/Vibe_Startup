using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ngibrid.Models;

/// <summary>
/// Payment entity - stores payment information
/// </summary>
public class Payment : BaseEntity
{
    public long OrderId { get; set; }

    [Required, MaxLength(100)]
    public string PaymentNumber { get; set; } = string.Empty;

    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "CASH"; // CASH, BANK_TRANSFER, E_WALLET, CREDIT_CARD, COD

    [MaxLength(50)]
    public string PaymentChannel { get; set; } = string.Empty; // GoPay, OVO, BCA, Mandiri, etc.

    public decimal Amount { get; set; }
    public decimal? AdminFee { get; set; }
    public decimal TotalAmount { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "PENDING"; // PENDING, PAID, FAILED, REFUNDED, PARTIAL

    public DateTime? PaidAt { get; set; }

    [MaxLength(100)]
    public string? TransactionId { get; set; } // From payment gateway

    [MaxLength(500)]
    public string? PaymentUrl { get; set; } // Payment gateway redirect URL

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(500)]
    public string? ProofOfPaymentUrl { get; set; }

    public DateTime? ExpiredAt { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }
}

/// <summary>
/// Invoice entity
/// </summary>
public class Invoice : BaseEntity
{
    public long OrderId { get; set; }

    [Required, MaxLength(100)]
    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }

    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal InsuranceFee { get; set; }
    public decimal TotalAmount { get; set; }

    [MaxLength(10)]
    public string Currency { get; set; } = "IDR";

    [MaxLength(50)]
    public string Status { get; set; } = "UNPAID"; // UNPAID, PAID, OVERDUE, CANCELLED

    [MaxLength(500)]
    public string? EReceiptUrl { get; set; }

    [MaxLength(500)]
    public string? PdfUrl { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }
}

/// <summary>
/// Insurance claim entity
/// </summary>
public class InsuranceClaim : BaseEntity
{
    public long OrderId { get; set; }

    [Required, MaxLength(100)]
    public string ClaimNumber { get; set; } = string.Empty;

    public decimal ClaimAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "SUBMITTED"; // SUBMITTED, REVIEWING, APPROVED, REJECTED, PAID

    [MaxLength(2000)]
    public string ClaimReason { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? SupportingDocumentUrl { get; set; }

    public DateTime? ReviewedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    [ForeignKey(nameof(OrderId))]
    public Order? Order { get; set; }
}

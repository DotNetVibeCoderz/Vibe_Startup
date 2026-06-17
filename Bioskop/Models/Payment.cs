using System.ComponentModel.DataAnnotations;

namespace Bioskop.Models;

/// <summary>
/// Data pembayaran untuk pesanan
/// </summary>
public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }

    [Required, MaxLength(100)]
    public string PaymentMethod { get; set; } = string.Empty; // EWallet, CreditCard, BankTransfer, QRIS

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Success, Failed

    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string? TransactionId { get; set; } // ID dari payment gateway

    [MaxLength(500)]
    public string? PaymentResponse { get; set; } // Raw response dari gateway (JSON)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public Order? Order { get; set; }
}

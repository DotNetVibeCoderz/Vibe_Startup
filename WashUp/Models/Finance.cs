using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WashUp.Models;

/// <summary>
/// Tracks order status changes for audit trail
/// </summary>
public class OrderStatusLog
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    
    [MaxLength(30)]
    public string OldStatus { get; set; } = string.Empty;
    
    [MaxLength(30)]
    public string NewStatus { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public string? ChangedByUserId { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Invoice generated for each order
/// </summary>
public class Invoice
{
    public int Id { get; set; }
    
    [Required, MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty; // INV-{date}-{seq}
    
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Subtotal { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Discount { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; } // PPh 10% or custom
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }
    
    [MaxLength(30)]
    public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid, Paid, Partial, Cancelled
    
    [MaxLength(30)]
    public string? PaymentMethod { get; set; }
    
    public DateTime? PaidAt { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [MaxLength(500)]
    public string? Notes { get; set; }
}

/// <summary>
/// Financial transaction record
/// </summary>
public class FinancialTransaction
{
    public int Id { get; set; }
    
    [Required, MaxLength(50)]
    public string TransactionType { get; set; } = string.Empty; // Income, Expense
    
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty; // OrderPayment, Supplies, Salary, Utility, Tax, etc.
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }
    
    public int? OrderId { get; set; }
    public int? BranchId { get; set; }
    
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string? RecordedByUserId { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
}

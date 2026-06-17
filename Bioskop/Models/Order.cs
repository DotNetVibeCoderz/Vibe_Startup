using System.ComponentModel.DataAnnotations;

namespace Bioskop.Models;

/// <summary>
/// Pesanan tiket bioskop
/// </summary>
public class Order
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string OrderNumber { get; set; } = string.Empty; // Format: BSK-20250101-XXXX

    public string UserId { get; set; } = string.Empty;
    public int ShowtimeId { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Paid, Cancelled, Completed, Expired

    public decimal Subtotal { get; set; } // Total tiket
    public decimal SnackTotal { get; set; } // Total snack
    public decimal TaxAmount { get; set; } // Pajak
    public decimal GrandTotal { get; set; } // Total keseluruhan

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime? PaidAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Navigation
    public ApplicationUser? User { get; set; }
    public Showtime? Showtime { get; set; }
    public Payment? Payment { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public ICollection<OrderSnack> OrderSnacks { get; set; } = new List<OrderSnack>();
}

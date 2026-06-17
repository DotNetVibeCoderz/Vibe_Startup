using System.ComponentModel.DataAnnotations;

namespace Bioskop.Models;

/// <summary>
/// Tiket individual dalam satu pesanan
/// </summary>
public class Ticket
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string TicketNumber { get; set; } = string.Empty; // Format: TKT-XXXXXXXX

    public int OrderId { get; set; }
    public int SeatId { get; set; }
    public int ShowtimeId { get; set; }

    [Required, MaxLength(100)]
    public string QrCode { get; set; } = string.Empty; // Unique QR Code data

    public decimal Price { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Active"; // Active, Used, Cancelled

    public DateTime? UsedAt { get; set; } // Kapan tiket discan
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Order? Order { get; set; }
    public Seat? Seat { get; set; }
    public Showtime? Showtime { get; set; }
}

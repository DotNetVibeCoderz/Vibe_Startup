using System.ComponentModel.DataAnnotations;

namespace Bioskop.Models;

/// <summary>
/// Kursi dalam studio
/// </summary>
public class Seat
{
    public int Id { get; set; }
    public int StudioId { get; set; }

    [Required, MaxLength(5)]
    public string RowLabel { get; set; } = "A"; // A, B, C, ...

    public int ColumnNumber { get; set; } // 1, 2, 3, ...

    /// <summary>
    /// Tipe kursi: Regular, Premium, VIP, Couple
    /// </summary>
    [MaxLength(20)]
    public string SeatType { get; set; } = "Regular";

    /// <summary>
    /// Multiplier harga berdasarkan tipe kursi
    /// </summary>
    public decimal PriceMultiplier { get; set; } = 1.0m;

    /// <summary>
    /// Status kursi: Available, Reserved, Maintenance
    /// </summary>
    [MaxLength(20)]
    public string Status { get; set; } = "Available";

    public bool IsActive { get; set; } = true;

    // Navigation
    public Studio? Studio { get; set; }
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}

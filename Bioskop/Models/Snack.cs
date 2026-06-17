using System.ComponentModel.DataAnnotations;

namespace Bioskop.Models;

/// <summary>
/// Menu makanan/minuman di bioskop
/// </summary>
public class Snack
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public decimal Price { get; set; }

    [MaxLength(50)]
    public string? Category { get; set; } // Popcorn, Minuman, Snack, Combo

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public bool IsAvailable { get; set; } = true;
    public int Stock { get; set; } = 100;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<OrderSnack> OrderSnacks { get; set; } = new List<OrderSnack>();
}

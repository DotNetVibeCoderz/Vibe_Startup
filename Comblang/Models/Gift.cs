using System.ComponentModel.DataAnnotations;

namespace Comblang.Models;

/// <summary>
/// Virtual gift catalog item available in the store.
/// </summary>
public class Gift
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(500)]
    public string IconUrl { get; set; } = string.Empty;

    public int Price { get; set; } = 50; // Virtual coins

    [MaxLength(50)]
    public string Category { get; set; } = "General"; // "Romantic", "Funny", "Premium", "Seasonal"

    public bool IsActive { get; set; } = true;
}

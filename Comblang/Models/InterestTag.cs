using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Comblang.Models;

/// <summary>
/// Interest/hobby tags for user profiles to enable better matching.
/// </summary>
public class InterestTag
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string TagName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Category { get; set; } = "General"; // "Hobby", "Music", "Sport", "Food", "Travel", "Lifestyle"

    // Navigation
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Comblang.Models;

/// <summary>
/// Block relationship between users for safety.
/// </summary>
public class UserBlock
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid BlockerId { get; set; }

    [Required]
    public Guid BlockedUserId { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }

    public DateTime BlockedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(BlockerId))]
    public User? Blocker { get; set; }

    [ForeignKey(nameof(BlockedUserId))]
    public User? BlockedUser { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace Comblang.Models;

/// <summary>
/// Public or thematic group chat rooms.
/// </summary>
public class GroupRoom
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string Category { get; set; } = "General"; // "General", "Hobby", "Travel", "Food", etc.

    [MaxLength(500)]
    public string? IconUrl { get; set; }

    public Guid CreatedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public int MaxMembers { get; set; } = 500;

    public ICollection<Message> Messages { get; set; } = new List<Message>();
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
}

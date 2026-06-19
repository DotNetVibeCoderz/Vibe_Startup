using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Comblang.Models;

public class GroupMember
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid GroupRoomId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [MaxLength(20)]
    public string Role { get; set; } = "Member"; // "Admin", "Moderator", "Member"

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(GroupRoomId))]
    public GroupRoom? GroupRoom { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}

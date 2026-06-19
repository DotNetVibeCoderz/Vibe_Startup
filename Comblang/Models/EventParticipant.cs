using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Comblang.Models;

public class EventParticipant
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid EventId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}

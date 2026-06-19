using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Comblang.Models;

public class GiftTransaction
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid GiftId { get; set; }

    [Required]
    public Guid SenderId { get; set; }

    [Required]
    public Guid ReceiverId { get; set; }

    [MaxLength(500)]
    public string? Message { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(GiftId))]
    public Gift? Gift { get; set; }

    [ForeignKey(nameof(SenderId))]
    public User? Sender { get; set; }

    [ForeignKey(nameof(ReceiverId))]
    public User? Receiver { get; set; }
}

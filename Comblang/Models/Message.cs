using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Comblang.Models;

/// <summary>
/// Chat message between users (direct or group).
/// </summary>
public class Message
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SenderId { get; set; }

    public Guid? ReceiverId { get; set; } // Null for group messages

    public Guid? GroupRoomId { get; set; }

    [MaxLength(50)]
    public string MessageType { get; set; } = "Text"; // "Text", "Image", "Video", "Audio", "Gift", "VoiceNote"

    [MaxLength(5000)]
    public string? Content { get; set; }

    [MaxLength(1000)]
    public string? MediaUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(SenderId))]
    public User? Sender { get; set; }

    [ForeignKey(nameof(ReceiverId))]
    public User? Receiver { get; set; }

    [ForeignKey(nameof(GroupRoomId))]
    public GroupRoom? GroupRoom { get; set; }
}

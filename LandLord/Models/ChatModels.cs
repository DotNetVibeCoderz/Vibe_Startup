using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LandLord.Models;

/// <summary>
/// Model untuk sesi chat
/// </summary>
public class ChatSession
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Display(Name = "Judul Sesi")]
    public string Title { get; set; } = "Chat Baru";

    [MaxLength(100)]
    public string? UserId { get; set; }

    [Display(Name = "Dibuat Pada")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Terakhir Update")]
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    // Navigation property
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

/// <summary>
/// Model untuk pesan chat
/// </summary>
public class ChatMessage
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int ChatSessionId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = "user"; // user, assistant, system

    [Required]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "Dikirim Pada")]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    [MaxLength(1000)]
    [Display(Name = "URL Gambar")]
    public string? ImageUrl { get; set; }

    [MaxLength(1000)]
    [Display(Name = "URL Dokumen")]
    public string? DocumentUrl { get; set; }

    [MaxLength(200)]
    [Display(Name = "Nama Dokumen")]
    public string? DocumentName { get; set; }

    [Display(Name = "Token Count")]
    public int? TokenCount { get; set; }

    // Navigation property
    [ForeignKey(nameof(ChatSessionId))]
    public ChatSession? ChatSession { get; set; }
}

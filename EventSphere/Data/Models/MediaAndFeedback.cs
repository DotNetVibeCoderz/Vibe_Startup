using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventSphere.Data.Models;

/// <summary>
/// Tata letak meja dan kursi
/// </summary>
public class TableArrangement
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid EventId { get; set; }
    
    [Required, MaxLength(100)]
    public string TableName { get; set; } = string.Empty; // "Table 1", "VIP Table", etc.
    
    [MaxLength(50)]
    public string? Shape { get; set; } = "Round"; // Round, Rectangle, Square, Oval
    
    public int Capacity { get; set; } = 8;
    public int FilledSeats { get; set; }
    
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    
    public int SortOrder { get; set; }
    
    [MaxLength(50)]
    public string? Color { get; set; } = "#DFE6E9";
    
    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }
    
    public ICollection<EventAttendee> Attendees { get; set; } = new List<EventAttendee>();
}

/// <summary>
/// Media/foto/video gallery
/// </summary>
public class MediaItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid EventId { get; set; }
    
    [Required, MaxLength(500)]
    public string Url { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? ThumbnailUrl { get; set; }
    
    [MaxLength(50)]
    public string MediaType { get; set; } = "Image"; // Image, Video, Audio
    
    [MaxLength(200)]
    public string? Caption { get; set; }
    
    public string? UploadedById { get; set; }
    
    [MaxLength(50)]
    public string? Category { get; set; } // Ceremony, Reception, Candid, etc.
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }
    
    [ForeignKey(nameof(UploadedById))]
    public ApplicationUser? UploadedBy { get; set; }
}

/// <summary>
/// Dokumen (kontrak, proposal, dll)
/// </summary>
public class Document
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required, MaxLength(300)]
    public string FileName { get; set; } = string.Empty;
    
    [Required, MaxLength(500)]
    public string FileUrl { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string? FileType { get; set; } // pdf, docx, xlsx, etc.
    
    public long FileSize { get; set; }
    
    public Guid? EventId { get; set; }
    
    [MaxLength(100)]
    public string? Category { get; set; } // Contract, Proposal, Invoice, etc.
    
    public string? UploadedById { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }
    
    [ForeignKey(nameof(UploadedById))]
    public ApplicationUser? UploadedBy { get; set; }
}

/// <summary>
/// Feedback/survei
/// </summary>
public class Feedback
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    public Guid? EventId { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public int Rating { get; set; } // 1-5
    
    [MaxLength(2000)]
    public string? Comment { get; set; }
    
    [MaxLength(100)]
    public string? Category { get; set; } // Overall, Food, Decoration, Music, Service
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }
    
    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}

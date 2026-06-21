using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentalBoil.Models;

/// <summary>
/// Model Review/Ulasan kendaraan
/// </summary>
public class Review
{
    public int Id { get; set; }
    public int VehicleId { get; set; }
    public int? BookingId { get; set; }

    /// <summary>
    /// User yang memberikan review
    /// </summary>
    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Rating 1-5
    /// </summary>
    [Range(1, 5)]
    public int Rating { get; set; }

    /// <summary>
    /// Komentar review
    /// </summary>
    [MaxLength(2000)]
    public string? Comment { get; set; }

    /// <summary>
    /// Foto yang dilampirkan (JSON array URLs)
    /// </summary>
    public string? Photos { get; set; }

    /// <summary>
    /// Tanggal review
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Apakah review diverifikasi (dari booking asli)
    /// </summary>
    public bool IsVerified { get; set; }

    // Navigation
    [ForeignKey(nameof(VehicleId))]
    public Vehicle? Vehicle { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [ForeignKey(nameof(BookingId))]
    public Booking? Booking { get; set; }
}

/// <summary>
/// Model Notifikasi untuk user
/// </summary>
public class Notification
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Judul notifikasi
    /// </summary>
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Isi notifikasi
    /// </summary>
    [MaxLength(2000)]
    public string? Message { get; set; }

    /// <summary>
    /// Tipe notifikasi
    /// </summary>
    [MaxLength(50)]
    public string Type { get; set; } = "info"; // info, warning, success, danger

    /// <summary>
    /// URL untuk navigasi
    /// </summary>
    [MaxLength(500)]
    public string? Link { get; set; }

    /// <summary>
    /// Apakah sudah dibaca
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Tanggal dibuat
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tanggal dibaca
    /// </summary>
    public DateTime? ReadAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}

/// <summary>
/// Model Chat antara Partner dan Customer
/// </summary>
public class ChatMessage
{
    public int Id { get; set; }

    /// <summary>
    /// ID booking terkait
    /// </summary>
    public int? BookingId { get; set; }

    /// <summary>
    /// Pengirim
    /// </summary>
    [Required, MaxLength(450)]
    public string SenderId { get; set; } = string.Empty;

    /// <summary>
    /// Penerima
    /// </summary>
    [Required, MaxLength(450)]
    public string ReceiverId { get; set; } = string.Empty;

    /// <summary>
    /// Isi pesan
    /// </summary>
    [Required, MaxLength(5000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Apakah sudah dibaca
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Tanggal dikirim
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tanggal dibaca
    /// </summary>
    public DateTime? ReadAt { get; set; }

    // Navigation
    [ForeignKey(nameof(SenderId))]
    public ApplicationUser? Sender { get; set; }

    [ForeignKey(nameof(ReceiverId))]
    public ApplicationUser? Receiver { get; set; }

    [ForeignKey(nameof(BookingId))]
    public Booking? Booking { get; set; }
}

/// <summary>
/// Model Promosi/Diskon
/// </summary>
public class Promotion
{
    public int Id { get; set; }

    /// <summary>
    /// Kode kupon
    /// </summary>
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Deskripsi promosi
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Tipe diskon: percentage / fixed
    /// </summary>
    [Required, MaxLength(20)]
    public string DiscountType { get; set; } = "percentage";

    /// <summary>
    /// Nilai diskon (persentase atau nominal)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountValue { get; set; }

    /// <summary>
    /// Minimal transaksi
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal MinTransaction { get; set; }

    /// <summary>
    /// Maksimal diskon
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaxDiscount { get; set; }

    /// <summary>
    /// Tanggal mulai berlaku
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Tanggal berakhir
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Kuota penggunaan
    /// </summary>
    public int? UsageLimit { get; set; }

    /// <summary>
    /// Sudah terpakai berapa kali
    /// </summary>
    public int UsageCount { get; set; }

    /// <summary>
    /// Status aktif
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Untuk membership tier tertentu (null = semua)
    /// </summary>
    [MaxLength(50)]
    public string? RequiredTier { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Model Loyalty/Point transaksi
/// </summary>
public class LoyaltyTransaction
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Jumlah poin
    /// </summary>
    public int Points { get; set; }

    /// <summary>
    /// Tipe transaksi: earn, redeem, bonus
    /// </summary>
    [MaxLength(20)]
    public string Type { get; set; } = "earn";

    /// <summary>
    /// Deskripsi
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Referensi booking jika terkait
    /// </summary>
    public int? BookingId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }
}

/// <summary>
/// Model FAQ (Frequently Asked Questions)
/// </summary>
public class Faq
{
    public int Id { get; set; }

    [Required, MaxLength(500)]
    public string Question { get; set; } = string.Empty;

    [Required, MaxLength(4000)]
    public string Answer { get; set; } = string.Empty;

    /// <summary>
    /// Kategori FAQ
    /// </summary>
    [MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Urutan tampilan
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Bahasa (id/en)
    /// </summary>
    [MaxLength(5)]
    public string Language { get; set; } = "id";

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Model Pengaturan Sistem
/// </summary>
public class SystemSetting
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Nilai setting
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Grup setting
    /// </summary>
    [MaxLength(100)]
    public string? Group { get; set; }

    /// <summary>
    /// Deskripsi setting
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Model untuk Chat Session Bot
/// </summary>
public class ChatSession
{
    public int Id { get; set; }

    [Required, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Judul sesi chat
    /// </summary>
    [MaxLength(200)]
    public string? Title { get; set; }

    /// <summary>
    /// Model AI yang digunakan
    /// </summary>
    [MaxLength(50)]
    public string? Model { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    public ICollection<ChatHistory> Messages { get; set; } = new List<ChatHistory>();
}

/// <summary>
/// Model Riwayat Chat Bot
/// </summary>
public class ChatHistory
{
    public int Id { get; set; }
    public int SessionId { get; set; }

    /// <summary>
    /// Role: user, assistant, system
    /// </summary>
    [Required, MaxLength(20)]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Konten pesan (bisa Markdown)
    /// </summary>
    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Konten gambar (URLs, JSON array)
    /// </summary>
    public string? ImageUrls { get; set; }

    /// <summary>
    /// Konten dokumen (URLs, JSON array)
    /// </summary>
    public string? DocumentUrls { get; set; }

    /// <summary>
    /// Token yang terpakai
    /// </summary>
    public int? TokenCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(SessionId))]
    public ChatSession? Session { get; set; }
}

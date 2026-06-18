using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LandLord.Models;

/// <summary>
/// Model untuk dokumen/lampiran terkait tanah atau bangunan
/// </summary>
public class Document
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    [Display(Name = "Nama File")]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    [Display(Name = "Path File")]
    public string FilePath { get; set; } = string.Empty;

    [MaxLength(100)]
    [Display(Name = "Tipe Konten")]
    public string ContentType { get; set; } = string.Empty;

    [Display(Name = "Ukuran File (bytes)")]
    public long FileSize { get; set; }

    [MaxLength(20)]
    [Display(Name = "Kategori")]
    public string? Kategori { get; set; }
    // Gambar, Video, PDF, Dokumen, Lainnya

    [MaxLength(500)]
    [Display(Name = "Deskripsi")]
    public string? Deskripsi { get; set; }

    [Display(Name = "Tanggal Upload")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    [Display(Name = "Diupload Oleh")]
    public string? UploadedBy { get; set; }

    // Foreign keys (nullable, bisa terkait tanah atau bangunan)
    [Display(Name = "ID Tanah")]
    public int? TanahId { get; set; }

    [Display(Name = "ID Bangunan")]
    public int? BangunanId { get; set; }

    // Navigation properties
    [ForeignKey(nameof(TanahId))]
    public Tanah? Tanah { get; set; }

    [ForeignKey(nameof(BangunanId))]
    public Bangunan? Bangunan { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LandLord.Models;

/// <summary>
/// Model untuk data tanah sesuai standar metadata Indonesia
/// </summary>
public class Tanah
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "Nomor Sertifikat wajib diisi")]
    [MaxLength(100)]
    [Display(Name = "Nomor Sertifikat")]
    public string NomorSertifikat { get; set; } = string.Empty;

    [Required(ErrorMessage = "Jenis Hak wajib dipilih")]
    [MaxLength(50)]
    [Display(Name = "Jenis Hak")]
    public string JenisHak { get; set; } = string.Empty;
    // Jenis Hak: Hak Milik, Hak Guna Bangunan, Hak Guna Usaha, Hak Pakai, Hak Sewa

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Luas Tanah (m²)")]
    public decimal Luas { get; set; }

    [Required(ErrorMessage = "Lokasi wajib diisi")]
    [MaxLength(500)]
    public string Lokasi { get; set; } = string.Empty;

    [MaxLength(50)]
    [Display(Name = "NIB")]
    public string? NIB { get; set; } // Nomor Identifikasi Bidang

    [MaxLength(100)]
    [Display(Name = "Kelurahan")]
    public string? Kelurahan { get; set; }

    [MaxLength(100)]
    [Display(Name = "Kecamatan")]
    public string? Kecamatan { get; set; }

    [MaxLength(100)]
    [Display(Name = "Kota/Kabupaten")]
    public string? KotaKabupaten { get; set; }

    [MaxLength(50)]
    [Display(Name = "Provinsi")]
    public string? Provinsi { get; set; }

    [MaxLength(10)]
    [Display(Name = "Kode Pos")]
    public string? KodePos { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    [Display(Name = "Latitude")]
    public double? Latitude { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    [Display(Name = "Longitude")]
    public double? Longitude { get; set; }

    [Display(Name = "Koordinat Polygon (GeoJSON)")]
    public string? PolygonGeoJson { get; set; } // Format GeoJSON untuk polygon bidang tanah

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Nilai NJOP per m²")]
    public decimal? NilaiNjopPerMeter { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Total NJOP")]
    public decimal? TotalNjop { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Pajak Tahunan")]
    public decimal? PajakTahunan { get; set; }

    [Display(Name = "Status Pajak")]
    [MaxLength(20)]
    public string? StatusPajak { get; set; } = "Lunas"; // Lunas, Menunggak, Bebas

    [Required]
    [MaxLength(200)]
    [Display(Name = "Pemilik")]
    public string Pemilik { get; set; } = string.Empty;

    [MaxLength(50)]
    [Display(Name = "NIK Pemilik")]
    public string? NikPemilik { get; set; }

    [MaxLength(200)]
    [Display(Name = "Alamat Pemilik")]
    public string? AlamatPemilik { get; set; }

    [MaxLength(500)]
    [Display(Name = "Keterangan")]
    public string? Keterangan { get; set; }

    [Display(Name = "Tanggal Sertifikat")]
    public DateTime? TanggalSertifikat { get; set; }

    [Display(Name = "Tanggal Dibuat")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Display(Name = "Tanggal Diupdate")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    [Display(Name = "Dibuat Oleh")]
    public string? CreatedBy { get; set; }

    // Navigation property
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}

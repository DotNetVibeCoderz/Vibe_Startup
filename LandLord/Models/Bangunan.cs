using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LandLord.Models;

/// <summary>
/// Model untuk data bangunan sesuai standar metadata Indonesia
/// </summary>
public class Bangunan
{
    [Key]
    public int Id { get; set; }

    [Required(ErrorMessage = "Nomor IMB/PBG wajib diisi")]
    [MaxLength(100)]
    [Display(Name = "Nomor IMB/PBG")]
    public string NomorIimbPbg { get; set; } = string.Empty;

    [MaxLength(50)]
    [Display(Name = "Nomor Sertifikat Tanah")]
    public string? NomorSertifikatTanah { get; set; }

    [Required(ErrorMessage = "Jenis Bangunan wajib dipilih")]
    [MaxLength(100)]
    [Display(Name = "Jenis Bangunan")]
    public string JenisBangunan { get; set; } = string.Empty;
    // Rumah Tinggal, Ruko, Gudang, Kantor, Apartemen, Pabrik, Hotel, Mall, dll

    [Required]
    [Display(Name = "Jumlah Lantai")]
    public int JumlahLantai { get; set; } = 1;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Luas Bangunan (m²)")]
    public decimal LuasBangunan { get; set; }

    [MaxLength(200)]
    [Display(Name = "Material Utama")]
    public string? MaterialUtama { get; set; }
    // Beton, Kayu, Baja, Bambu, Bata, dll

    [Required]
    [Display(Name = "Tahun Pembangunan")]
    public int TahunPembangunan { get; set; }

    [MaxLength(200)]
    [Display(Name = "Fungsi Bangunan")]
    public string? FungsiBangunan { get; set; }
    // Hunian, Komersial, Industri, Pendidikan, Kesehatan, Pemerintahan, Ibadah

    [MaxLength(200)]
    [Display(Name = "Kepemilikan")]
    public string? Kepemilikan { get; set; }
    // Pribadi, PT, CV, Yayasan, Pemerintah, BUMN

    [Required]
    [MaxLength(500)]
    [Display(Name = "Lokasi")]
    public string Lokasi { get; set; } = string.Empty;

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

    [Column(TypeName = "decimal(18,6)")]
    [Display(Name = "Latitude")]
    public double? Latitude { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    [Display(Name = "Longitude")]
    public double? Longitude { get; set; }

    [Display(Name = "Koordinat Polygon (GeoJSON)")]
    public string? PolygonGeoJson { get; set; }

    [MaxLength(500)]
    [Display(Name = "Nama Pemilik")]
    public string? NamaPemilik { get; set; }

    [MaxLength(50)]
    [Display(Name = "NIK Pemilik")]
    public string? NikPemilik { get; set; }

    [MaxLength(500)]
    [Display(Name = "Keterangan")]
    public string? Keterangan { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "Nilai Bangunan")]
    public decimal? NilaiBangunan { get; set; }

    [Display(Name = "Status")]
    [MaxLength(50)]
    public string? Status { get; set; } = "Aktif"; // Aktif, Dalam Perbaikan, Tidak Aktif, Terbengkalai

    [Display(Name = "Tanggal IMB/PBG")]
    public DateTime? TanggalIimbPbg { get; set; }

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

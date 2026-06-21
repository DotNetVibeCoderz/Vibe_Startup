using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentalBoil.Models;

/// <summary>
/// Model Kendaraan - inti dari sistem rental
/// </summary>
public class Vehicle
{
    public int Id { get; set; }

    /// <summary>
    /// Nama/model kendaraan (contoh: "Toyota Avanza 2024")
    /// </summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Plat nomor kendaraan
    /// </summary>
    [Required, MaxLength(20)]
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>
    /// Jenis kendaraan (Mobil/Motor)
    /// </summary>
    public VehicleType Type { get; set; }

    /// <summary>
    /// Merek kendaraan
    /// </summary>
    [MaxLength(100)]
    public string Brand { get; set; } = string.Empty;

    /// <summary>
    /// Model spesifik
    /// </summary>
    [MaxLength(100)]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Tahun produksi
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Warna kendaraan
    /// </summary>
    [MaxLength(50)]
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// Transmisi
    /// </summary>
    public TransmissionType Transmission { get; set; }

    /// <summary>
    /// Jenis bahan bakar
    /// </summary>
    public FuelType FuelType { get; set; }

    /// <summary>
    /// Kapasitas penumpang
    /// </summary>
    public int Capacity { get; set; }

    /// <summary>
    /// Kapasitas bagasi (liter)
    /// </summary>
    public int? LuggageCapacity { get; set; }

    /// <summary>
    /// Harga sewa per jam
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerHour { get; set; }

    /// <summary>
    /// Harga sewa per hari
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal PricePerDay { get; set; }

    /// <summary>
    /// Harga dinamis - multiplier saat peak season
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal DynamicPriceMultiplier { get; set; } = 1.0m;

    /// <summary>
    /// Deskripsi kendaraan (markdown supported)
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Spesifikasi dalam format JSON
    /// </summary>
    public string? Specifications { get; set; }

    /// <summary>
    /// Lokasi kendaraan (alamat)
    /// </summary>
    [MaxLength(500)]
    public string? Location { get; set; }

    /// <summary>
    /// Latitude untuk Google Maps
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Longitude untuk Google Maps
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Rating rata-rata
    /// </summary>
    public double AverageRating { get; set; }

    /// <summary>
    /// Jumlah review
    /// </summary>
    public int ReviewCount { get; set; }

    /// <summary>
    /// Jumlah kali disewa
    /// </summary>
    public int RentalCount { get; set; }

    /// <summary>
    /// Apakah kendaraan tersedia
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Apakah sudah diverifikasi admin
    /// </summary>
    public bool IsVerified { get; set; }

    /// <summary>
    /// Pemilik kendaraan (Partner)
    /// </summary>
    [MaxLength(450)]
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>
    /// Asuransi tersedia
    /// </summary>
    public bool InsuranceAvailable { get; set; }

    /// <summary>
    /// Biaya asuransi per hari
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal InsuranceCostPerDay { get; set; }

    /// <summary>
    /// Status IoT: status kunci
    /// </summary>
    public LockStatus LockStatus { get; set; } = LockStatus.Locked;

    /// <summary>
    /// Status IoT: status mesin
    /// </summary>
    public EngineStatus EngineStatus { get; set; } = EngineStatus.Off;

    /// <summary>
    /// Status GPS: bergerak/berhenti
    /// </summary>
    public VehicleMotionStatus MotionStatus { get; set; } = VehicleMotionStatus.Stopped;

    /// <summary>
    /// Speed saat ini (km/h) - dari GPS simulator
    /// </summary>
    public double CurrentSpeed { get; set; }

    /// <summary>
    /// Heading (derajat) - dari GPS simulator
    /// </summary>
    public double CurrentHeading { get; set; }

    /// <summary>
    /// Tanggal dibuat
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tanggal update terakhir
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(OwnerId))]
    public ApplicationUser? Owner { get; set; }
    public ICollection<VehiclePhoto> Photos { get; set; } = new List<VehiclePhoto>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<VehicleAvailability> Availabilities { get; set; } = new List<VehicleAvailability>();
    public ICollection<VehicleMaintenance> Maintenances { get; set; } = new List<VehicleMaintenance>();
}

/// <summary>
/// Foto kendaraan
/// </summary>
public class VehiclePhoto
{
    public int Id { get; set; }
    public int VehicleId { get; set; }

    /// <summary>
    /// URL atau path file foto
    /// </summary>
    [Required, MaxLength(500)]
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Apakah foto utama
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Urutan tampilan
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Judul foto
    /// </summary>
    [MaxLength(200)]
    public string? Caption { get; set; }

    [ForeignKey(nameof(VehicleId))]
    public Vehicle? Vehicle { get; set; }
}

/// <summary>
/// Kalender ketersediaan kendaraan (tanggal diblok)
/// </summary>
public class VehicleAvailability
{
    public int Id { get; set; }
    public int VehicleId { get; set; }

    /// <summary>
    /// Tanggal mulai diblok
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Tanggal selesai diblok
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Alasan pemblokiran
    /// </summary>
    [MaxLength(500)]
    public string? Reason { get; set; }

    /// <summary>
    /// Apakah karena booking (true) atau maintenance (false)
    /// </summary>
    public bool IsBooked { get; set; }

    [ForeignKey(nameof(VehicleId))]
    public Vehicle? Vehicle { get; set; }
}

/// <summary>
/// Riwayat maintenance kendaraan
/// </summary>
public class VehicleMaintenance
{
    public int Id { get; set; }
    public int VehicleId { get; set; }

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public DateTime MaintenanceDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Cost { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [ForeignKey(nameof(VehicleId))]
    public Vehicle? Vehicle { get; set; }
}

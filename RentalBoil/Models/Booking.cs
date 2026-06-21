using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RentalBoil.Models;

/// <summary>
/// Model Booking/Pemesanan kendaraan
/// </summary>
public class Booking
{
    public int Id { get; set; }

    /// <summary>
    /// Nomor booking unik (format: RB-YYYYMMDD-XXXX)
    /// </summary>
    [Required, MaxLength(50)]
    public string BookingNumber { get; set; } = string.Empty;

    public int VehicleId { get; set; }

    /// <summary>
    /// ID Customer yang memesan
    /// </summary>
    [Required, MaxLength(450)]
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Status booking
    /// </summary>
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    /// <summary>
    /// Tanggal mulai sewa
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Tanggal selesai sewa
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Total durasi dalam jam
    /// </summary>
    public double DurationHours { get; set; }

    /// <summary>
    /// Total durasi dalam hari
    /// </summary>
    public int DurationDays { get; set; }

    /// <summary>
    /// Harga dasar sebelum add-on
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal BasePrice { get; set; }

    /// <summary>
    /// Biaya asuransi
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal InsuranceCost { get; set; }

    /// <summary>
    /// Diskon yang diterapkan
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Discount { get; set; }

    /// <summary>
    /// Kode kupon jika ada
    /// </summary>
    [MaxLength(50)]
    public string? CouponCode { get; set; }

    /// <summary>
    /// Total harga final
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalPrice { get; set; }

    /// <summary>
    /// Status pembayaran
    /// </summary>
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    /// <summary>
    /// Metode pembayaran
    /// </summary>
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>
    /// Tanggal pembayaran
    /// </summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>
    /// Alamat pickup
    /// </summary>
    [MaxLength(500)]
    public string? PickupAddress { get; set; }

    /// <summary>
    /// Latitude pickup
    /// </summary>
    public double? PickupLatitude { get; set; }

    /// <summary>
    /// Longitude pickup
    /// </summary>
    public double? PickupLongitude { get; set; }

    /// <summary>
    /// Alamat drop-off
    /// </summary>
    [MaxLength(500)]
    public string? DropoffAddress { get; set; }

    /// <summary>
    /// Latitude drop-off
    /// </summary>
    public double? DropoffLatitude { get; set; }

    /// <summary>
    /// Longitude drop-off
    /// </summary>
    public double? DropoffLongitude { get; set; }

    /// <summary>
    /// Catatan dari customer
    /// </summary>
    [MaxLength(1000)]
    public string? CustomerNotes { get; set; }

    /// <summary>
    /// Alasan penolakan (jika ditolak)
    /// </summary>
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Tanggal booking dibuat
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tanggal update terakhir
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(VehicleId))]
    public Vehicle? Vehicle { get; set; }

    [ForeignKey(nameof(CustomerId))]
    public ApplicationUser? Customer { get; set; }

    public Payment? Payment { get; set; }
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}

/// <summary>
/// Model Pembayaran
/// </summary>
public class Payment
{
    public int Id { get; set; }
    public int BookingId { get; set; }

    /// <summary>
    /// ID transaksi eksternal
    /// </summary>
    [MaxLength(100)]
    public string? ExternalTransactionId { get; set; }

    /// <summary>
    /// Metode pembayaran
    /// </summary>
    public PaymentMethod Method { get; set; }

    /// <summary>
    /// Jumlah dibayar
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Status pembayaran
    /// </summary>
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Tanggal pembayaran
    /// </summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>
    /// Tanggal kadaluarsa pembayaran
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Bukti pembayaran (URL/file)
    /// </summary>
    [MaxLength(500)]
    public string? PaymentProof { get; set; }

    /// <summary>
    /// Data tambahan (JSON)
    /// </summary>
    public string? Metadata { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(BookingId))]
    public Booking? Booking { get; set; }
}

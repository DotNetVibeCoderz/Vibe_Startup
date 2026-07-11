using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartDrive.Models.Enums;

namespace SmartDrive.Models.Entities;

/// <summary>
/// Data kendaraan latihan
/// </summary>
public class Vehicle
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string PlateNumber { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Brand { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Model { get; set; } = string.Empty;

    public int Year { get; set; }

    public TransmissionType Transmission { get; set; }

    [MaxLength(30)]
    public string? Color { get; set; }

    [MaxLength(50)]
    public string? ChassisNumber { get; set; }

    public VehicleStatus Status { get; set; } = VehicleStatus.Available;

    public int TotalHoursUsed { get; set; }

    public DateTime? LastServiceDate { get; set; }

    public DateTime? NextServiceDate { get; set; }

    public int ServiceIntervalKm { get; set; } = 5000;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(255)]
    public string? ImagePath { get; set; }

    public bool HasInsurance { get; set; } = true;

    public DateTime? InsuranceExpiryDate { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<VehicleServiceRecord> ServiceRecords { get; set; } = new List<VehicleServiceRecord>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

/// <summary>
/// Riwayat servis kendaraan
/// </summary>
public class VehicleServiceRecord
{
    [Key]
    public int Id { get; set; }

    public int VehicleId { get; set; }

    [ForeignKey(nameof(VehicleId))]
    public Vehicle? Vehicle { get; set; }

    public DateTime ServiceDate { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Cost { get; set; }

    public ServiceStatus Status { get; set; }

    public int OdometerReading { get; set; }

    [MaxLength(100)]
    public string? ServiceProvider { get; set; }

    [MaxLength(255)]
    public string? InvoiceFilePath { get; set; }
}

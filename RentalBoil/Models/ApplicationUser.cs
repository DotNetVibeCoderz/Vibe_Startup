using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace RentalBoil.Models;

/// <summary>
/// Extended Application User dengan informasi tambahan untuk rental
/// </summary>
public class ApplicationUser : IdentityUser
{
    [MaxLength(100)]
    public string? FullName { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(50)]
    public string? City { get; set; }

    [MaxLength(20)]
    public string? PostalCode { get; set; }

    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Path ke foto profil
    /// </summary>
    [MaxLength(500)]
    public string? ProfilePhoto { get; set; }

    /// <summary>
    /// Path ke dokumen KTP
    /// </summary>
    [MaxLength(500)]
    public string? KtpDocument { get; set; }

    /// <summary>
    /// Status verifikasi KTP (null=belum upload, false=menunggu, true=terverifikasi)
    /// </summary>
    public bool? KtpVerified { get; set; }

    /// <summary>
    /// Path ke dokumen SIM
    /// </summary>
    [MaxLength(500)]
    public string? SimDocument { get; set; }

    /// <summary>
    /// Status verifikasi SIM
    /// </summary>
    public bool? SimVerified { get; set; }

    /// <summary>
    /// Poin loyalty member
    /// </summary>
    public int LoyaltyPoints { get; set; }

    /// <summary>
    /// Tipe membership
    /// </summary>
    [MaxLength(50)]
    public string? MembershipTier { get; set; } = "Basic";

    /// <summary>
    /// Akun di-suspend?
    /// </summary>
    public bool IsSuspended { get; set; }

    /// <summary>
    /// Tanggal registrasi
    /// </summary>
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User role dalam sistem
    /// </summary>
    public UserRole Role { get; set; } = UserRole.Customer;

    /// <summary>
    /// Preferensi bahasa (id/en)
    /// </summary>
    [MaxLength(5)]
    public string Language { get; set; } = "id";

    // Navigation properties
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<LoyaltyTransaction> LoyaltyTransactions { get; set; } = new List<LoyaltyTransaction>();
}

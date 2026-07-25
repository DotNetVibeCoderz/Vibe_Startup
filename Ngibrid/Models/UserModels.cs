using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Ngibrid.Models;

/// <summary>
/// Extended Application User with logistics-specific fields
/// </summary>
public class ApplicationUser : IdentityUser<long>
{
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? Province { get; set; }

    [MaxLength(10)]
    public string? PostalCode { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber2 { get; set; }

    public DateTime? DateOfBirth { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// User type: Customer, Courier, Admin, WarehouseStaff, Manager
    /// </summary>
    [MaxLength(50)]
    public string UserType { get; set; } = "Customer";

    public bool IsActive { get; set; } = true;

    public int LoyaltyPoints { get; set; } = 0;

    public string? RefreshToken { get; set; }

    public DateTime? RefreshTokenExpiry { get; set; }

    // Navigation properties
    public ICollection<Order>? Orders { get; set; }
    public ICollection<PickupRequest>? PickupRequests { get; set; }
    public CourierProfile? CourierProfile { get; set; }
}

/// <summary>
/// Application Role extending Identity Role
/// </summary>
public class ApplicationRole : IdentityRole<long>
{
    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// User-Role mapping
/// </summary>
public class ApplicationUserRole : IdentityUserRole<long>
{
    public virtual ApplicationUser? User { get; set; }
    public virtual ApplicationRole? Role { get; set; }
}

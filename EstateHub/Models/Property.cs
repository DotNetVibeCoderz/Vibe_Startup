using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EstateHub.Models;

/// <summary>
/// Property listing - core entity
/// </summary>
public class Property
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>House, Apartment, ShopHouse, Villa, Land, Office</summary>
    [Required, MaxLength(30)]
    public string PropertyType { get; set; } = "House";

    /// <summary>Sale, Rent</summary>
    [Required, MaxLength(10)]
    public string ListingType { get; set; } = "Sale";

    /// <summary>Available, Sold, Rented, Pending</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = "Available";

    public decimal Price { get; set; }
    public decimal? PricePerSqm { get; set; }

    public double LandArea { get; set; } // m²
    public double BuildingArea { get; set; } // m²

    public int Bedrooms { get; set; }
    public int Bathrooms { get; set; }
    public int Floors { get; set; } = 1;
    public int? YearBuilt { get; set; }

    [MaxLength(300)]
    public string? Facilities { get; set; } // Comma-separated: pool,gym,garden,parking

    [MaxLength(300)]
    public string? NearbyFacilities { get; set; } // Schools,hospitals,malls

    // Location
    [Required, MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(100)]
    public string? District { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    [MaxLength(20)]
    public string? ZipCode { get; set; }

    // Media
    [MaxLength(500)]
    public string? MainImageUrl { get; set; }

    public string? ImageUrls { get; set; } // JSON array of URLs

    [MaxLength(500)]
    public string? VirtualTourUrl { get; set; }

    [MaxLength(500)]
    public string? VideoUrl { get; set; }

    // Owner/Agent
    [Required]
    public string OwnerId { get; set; } = string.Empty;

    [ForeignKey(nameof(OwnerId))]
    public ApplicationUser? Owner { get; set; }

    // Marketing
    public bool IsPremium { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime? PremiumUntil { get; set; }
    public int ViewCount { get; set; }

    // Certifications
    [MaxLength(100)]
    public string? CertificateNumber { get; set; }

    [MaxLength(100)]
    public string? IMBNumber { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsVerified { get; set; }

    // Navigation
    public ICollection<WishlistItem> WishlistedBy { get; set; } = new List<WishlistItem>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

// Activities & Events models
using Joka.Models.Common;

namespace Joka.Models.Activities;

public class Activity : BaseEntity
{
    /// <summary>Owning partner. Drives what a Merchant account may see and edit.</summary>
    public Guid? MerchantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "Attraction"; // Attraction, Concert, Tour, Workshop, Sports, Food
    public string City { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageUrls { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "IDR";
    public int DurationMinutes { get; set; } = 120;
    public DateTime? EventDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int TotalTickets { get; set; }
    public int SoldTickets { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public string? Includes { get; set; } // JSON
    public string? Terms { get; set; }
    public bool IsRefundable { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ActivityBooking : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ActivityId { get; set; }
    public Activity? Activity { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int TicketCount { get; set; } = 1;
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "IDR";
    public DateTime VisitDate { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTime BookingDate { get; set; } = DateTime.UtcNow;
    public string? ETicketUrl { get; set; }
    public string? QrCodeData { get; set; }
}


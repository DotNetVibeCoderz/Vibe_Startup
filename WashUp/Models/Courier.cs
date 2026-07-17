using System.ComponentModel.DataAnnotations;

namespace WashUp.Models;

/// <summary>
/// Courier delivery/pickup assignment for orders
/// </summary>
public class CourierAssignment
{
    public int Id { get; set; }
    
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    
    public int StaffMemberId { get; set; }
    public StaffMember? Courier { get; set; }
    
    [MaxLength(20)]
    public string AssignmentType { get; set; } = string.Empty; // Pickup, Delivery
    
    [MaxLength(30)]
    public string Status { get; set; } = "Assigned"; // Assigned, InTransit, Arrived, Completed, Cancelled
    
    public DateTime? AssignedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? EstimatedArrival { get; set; }
    
    // GPS tracking
    public double? CurrentLatitude { get; set; }
    public double? CurrentLongitude { get; set; }
    public double? DestinationLatitude { get; set; }
    public double? DestinationLongitude { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public ICollection<GpsTrackingLog> TrackingLogs { get; set; } = new List<GpsTrackingLog>();
}

/// <summary>
/// GPS tracking log for courier movement simulation
/// </summary>
public class GpsTrackingLog
{
    public int Id { get; set; }
    public int CourierAssignmentId { get; set; }
    public CourierAssignment? CourierAssignment { get; set; }
    
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double SpeedKmh { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

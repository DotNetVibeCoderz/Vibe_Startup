using System.ComponentModel.DataAnnotations;

namespace WashUp.Models;

/// <summary>
/// Multi-branch management model
/// </summary>
public class Branch
{
    public int Id { get; set; }
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Address { get; set; }
    
    [MaxLength(20)]
    public string? Phone { get; set; }
    
    [MaxLength(100)]
    public string? Email { get; set; }
    
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation
    public ICollection<ApplicationUser> Staff { get; set; } = new List<ApplicationUser>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    public ICollection<IoTDevice> IoTDevices { get; set; } = new List<IoTDevice>();
}

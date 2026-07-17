using System.ComponentModel.DataAnnotations;

namespace WashUp.Models;

/// <summary>
/// IoT device for monitoring washing machines, electricity, water
/// </summary>
public class IoTDevice
{
    public int Id { get; set; }
    
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string DeviceType { get; set; } = string.Empty; // MesinCuci, Listrik, Air, SensorSuhu
    
    [MaxLength(100)]
    public string? DeviceId { get; set; } // Physical device ID
    
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    
    public bool IsActive { get; set; } = true;
    public bool IsSimulated { get; set; } = true;
    
    [MaxLength(50)]
    public string Status { get; set; } = "Offline"; // Online, Offline, Running, Error
    
    public DateTime? LastReadingAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ICollection<IoTSensorReading> Readings { get; set; } = new List<IoTSensorReading>();
}

/// <summary>
/// IoT sensor reading data
/// </summary>
public class IoTSensorReading
{
    public int Id { get; set; }
    public int IoTDeviceId { get; set; }
    public IoTDevice? Device { get; set; }
    
    public double Value { get; set; } // Main reading value
    
    [MaxLength(50)]
    public string Unit { get; set; } = string.Empty; // kWh, Liter, Celsius, RPM, etc.
    
    public double? SecondaryValue { get; set; } // Secondary reading (e.g., humidity for temp sensor)
    
    [MaxLength(50)]
    public string? SecondaryUnit { get; set; }
    
    [MaxLength(30)]
    public string Status { get; set; } = "Normal"; // Normal, Warning, Critical
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

using System.ComponentModel.DataAnnotations;

namespace WashUp.Models;

/// <summary>
/// Inventory item for tracking supplies (detergent, fragrance, plastic, etc.)
/// </summary>
public class InventoryItem
{
    public int Id { get; set; }
    
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty; // Detergent, Fragrance, Plastic, Other
    
    [MaxLength(50)]
    public string Unit { get; set; } = "pcs"; // pcs, kg, liter, pack
    
    public double CurrentStock { get; set; }
    public double MinimumStock { get; set; } // Alert when below this
    public double MaximumStock { get; set; }
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRestockedAt { get; set; }
    
    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
}

/// <summary>
/// Stock in/out movement record
/// </summary>
public class StockMovement
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }
    
    [MaxLength(20)]
    public string MovementType { get; set; } = string.Empty; // In, Out
    
    public double Quantity { get; set; }
    
    [MaxLength(500)]
    public string? Notes { get; set; }
    
    public string? RecordedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
